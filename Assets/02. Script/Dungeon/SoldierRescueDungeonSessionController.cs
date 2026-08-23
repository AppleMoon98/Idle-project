using System.Collections.Generic;
using Character;
using Character.Events;
using Combat;
using Core;
using Dungeon.Events;
using Gacha;
using Managers;
using Rank;
using Services;
using Soldier;
using Stage;
using UnityEngine;
using War;

namespace Dungeon
{
    /// <summary>
    /// 병사 구출 던전(구역 점령전) 한 판의 진행을 관리한다. War 시스템의 WarStructure(점령 게이지 +
    /// 주변 몬스터 밀어내기)를 매 시도마다 서로 최소 거리를 유지한 채 랜덤 위치에 zoneCount개
    /// 생성한다. 제한시간 안에 구역을 전부 점령하면 클리어(병사 뽑기 재료 지급), 시간 초과나
    /// 플레이어 사망이면 실패(재도전/나가기 대기). StoneDungeonSessionController와 같은
    /// "StageSO/StageProgression은 건드리지 않는 오버레이" 골격이지만, 이 던전은 병사 동행이
    /// 금지되므로 진입 시 SoldierSpawner를 통해 이미 배치된 병사를 전부 비활성화하고 종료 시
    /// 되돌린다.
    ///
    /// War.Objectives.StructureCaptureObjective/War.WarBattleController는 재사용하지 않는다 —
    /// 둘 다 챕터 클라이맥스의 고정 배치를 전제하는데, 이 던전은 시도마다 위치가 랜덤이라
    /// "전부 점령됐는지" 판정만 인라인으로 다시 구현한다(War.WarStructure 컴포넌트/데이터 자체는
    /// 그대로 재사용).
    /// </summary>
    public sealed class SoldierRescueDungeonSessionController : MonoBehaviour, ITickable
    {
        private const int MaxPlacementAttemptsPerZone = 30;

        [SerializeField]
        private SoldierRescueDungeonConfigSO config;

        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private SoldierSpawner soldierSpawner;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private GameObject structurePrefab;

        [SerializeField]
        private CaptureZoneAutoNavigator captureNavigator;

        [SerializeField]
        private SoldierRescueSniperAttackSpawner sniperAttackSpawner;

        private readonly List<WarStructure> _activeZones = new();

        private int _stageNumber;
        private float _remainingTime;
        private bool _isActive;
        private bool _isFighting;
        private float _lastPublishedProgress;

        /// <summary>
        /// 오버레이가 진행 중인지(전투 중이든 실패 화면 대기 중이든) 여부.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// 실제로 전투/점령이 진행 중인지(실패 화면 대기 중이 아닌지) 여부. 화면 밖 점령지 화살표
        /// 안내(UI.SoldierRescueZoneIndicatorUI) 등, 실패 화면이 떠 있는 동안엔 표시할 필요가 없는
        /// UI가 이 값으로 게이트한다.
        /// </summary>
        public bool IsFighting => _isFighting;

        /// <summary>
        /// 현재 시도의 점령 구역 목록(읽기 전용). UI.SoldierRescueZoneIndicatorUI가 화면 밖 구역을
        /// 찾아 화살표로 안내하는 데 쓴다.
        /// </summary>
        public IReadOnlyList<WarStructure> ActiveZones => _activeZones;

        /// <summary>
        /// UI가 스테퍼의 최대 선택 가능 단계를 읽어가기 위한 접근자. Gold/Stone 던전과 동일한
        /// 이유·동일한 계산 — 플레이어가 실제로 클리어한 챕터를 기준으로 삼는다.
        /// </summary>
        public int MaxStageNumber
        {
            get
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService))
                {
                    return Mathf.Max(1, rankService.HighestClearedChapter);
                }

                return 1;
            }
        }

        /// <summary>
        /// 병사 구출 던전을 시작한다. 이미 진행 중이거나(자기 자신) 다른 오버레이가 이미 켜져
        /// 있으면(stageController.IsOverlayActive) 무시한다 — GoldDungeonSessionController.Enter와
        /// 동일한 이유(던전 중복 진입 방지).
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive) || config == null || structurePrefab == null)
            {
                return;
            }

            _isActive = true;
            _stageNumber = Mathf.Clamp(stageNumber, 1, MaxStageNumber);

            stageController?.PauseForOverlay($"병사 구출 {_stageNumber}층");
            stageController?.ResetCombatantsForRetry();
            stageController?.ResetSkillCooldowns();
            soldierSpawner?.SetSoldiersActive(false);

            StartAttempt();
        }

        /// <summary>
        /// 구출 실패 후 재도전한다. 진행 중이 아니거나 아직 전투 중이면 무시한다.
        /// </summary>
        public void Retry()
        {
            if (!_isActive || _isFighting)
            {
                return;
            }

            stageController?.ResetCombatantsForRetry();

            StartAttempt();
        }

        /// <summary>
        /// 구출 실패 후 나가기 — 원래 스테이지로 복귀한다. 전투 중이 아닐 때만 유효하다.
        /// </summary>
        public void ExitToOriginalStage()
        {
            if (!_isActive || _isFighting)
            {
                return;
            }

            _isActive = false;

            soldierSpawner?.SetSoldiersActive(true);
            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonSessionEndedEvent(false));
        }

        private void StartAttempt()
        {
            _remainingTime = config.TimeLimitSeconds;
            _isFighting = true;
            _lastPublishedProgress = -1f;

            SpawnZones();
            captureNavigator?.Activate(_activeZones);
            sniperAttackSpawner?.Activate(_stageNumber);

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonAttemptStartedEvent(_stageNumber, _remainingTime, _activeZones.Count));
        }

        private void SpawnZones()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(structurePrefab, config.ZoneCount, config.ZoneCount);

            Vector3[] positions = GenerateZonePositions();

            foreach (Vector3 position in positions)
            {
                GameObject instance = pool.Get(structurePrefab, position, Quaternion.identity);

                if (instance.TryGetComponent(out WarStructure structure))
                {
                    structure.ResetForNewAttempt();
                    _activeZones.Add(structure);
                }
            }
        }

        /// <summary>
        /// Services.CameraFollowService의 최광각(줌 배율과 무관한 고정 실제 플레이 구역) 경계 안에서,
        /// 서로 minDistanceBetweenZones 이상 떨어진 zoneCount개의 좌표를 시도해 생성한다. 서비스가
        /// 없으면(테스트 등) 원점 기준 적당한 기본 범위로 대체한다. 최대 시도 횟수 안에 조건을
        /// 만족하는 좌표를 못 찾은 구역은 마지막으로 시도한 좌표를 그대로 쓴다(입장 자체가
        /// 실패하지 않도록 하는 최선 노력 배치 — 아주 드물게만 최소 거리가 살짝 못 미칠 수 있다).
        /// 화면 아래쪽 하단 UI(메인 탭 바 등)가 실제 플레이 화면 일부를 가리는 만큼
        /// (Combat.SpawnGridLayout.BottomUiClearance — 병사 스폰 기준점이 이미 쓰는 것과 같은
        /// 값)은 세로 범위 하단에서 제외해, 구역이 그 뒤에 가려진 채로 스폰되는 일이 없게 한다.
        /// </summary>
        private Vector3[] GenerateZonePositions()
        {
            Vector3 center;
            Vector2 halfExtent;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraFollowService followService))
            {
                center = followService.HomeLocalPosition;
                halfExtent = followService.GetWorldBoundsHalfExtent();
            }
            else
            {
                center = Vector3.zero;
                halfExtent = new Vector2(8f, 16f);
            }

            float minY = center.y - halfExtent.y + SpawnGridLayout.BottomUiClearance;

            var positions = new Vector3[Mathf.Max(config.ZoneCount, 0)];

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 candidate = center;

                for (int attempt = 0; attempt < MaxPlacementAttemptsPerZone; attempt++)
                {
                    float x = Random.Range(center.x - halfExtent.x, center.x + halfExtent.x);
                    float y = Random.Range(minY, center.y + halfExtent.y);
                    candidate = new Vector3(x, y, 0f);

                    if (IsFarEnoughFromPrevious(candidate, positions, i))
                    {
                        break;
                    }
                }

                positions[i] = candidate;
            }

            return positions;
        }

        private bool IsFarEnoughFromPrevious(Vector3 candidate, Vector3[] positions, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Vector3.Distance(candidate, positions[i]) < config.MinDistanceBetweenZones)
                {
                    return false;
                }
            }

            return true;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isFighting)
            {
                return;
            }

            _remainingTime -= deltaTime;

            if (_remainingTime <= 0f)
            {
                HandleFailure();
                return;
            }

            TickZoneProgress();
        }

        /// <summary>
        /// 점령 구역 전부가 IsCaptured면 즉시 클리어. 그렇지 않으면 평균 진행도가 바뀔 때만
        /// SoldierRescueDungeonProgressChangedEvent를 발행한다(War.Objectives.StructureCaptureObjective의
        /// Progress01/IsCompleted와 동일한 계산을 인라인으로 다시 구현한 것).
        /// </summary>
        private void TickZoneProgress()
        {
            if (_activeZones.Count == 0)
            {
                return;
            }

            float total = 0f;
            bool allCaptured = true;

            foreach (WarStructure zone in _activeZones)
            {
                if (zone == null)
                {
                    allCaptured = false;
                    continue;
                }

                total += zone.Control;

                if (!zone.IsCaptured)
                {
                    allCaptured = false;
                }
            }

            float progress = total / _activeZones.Count;

            if (!Mathf.Approximately(progress, _lastPublishedProgress))
            {
                _lastPublishedProgress = progress;
                GameBootstrapper.Events?.Publish(new SoldierRescueDungeonProgressChangedEvent(progress));
            }

            if (allCaptured)
            {
                HandleClear();
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (playerTransform != null && evt.Character == playerTransform.gameObject)
            {
                HandleFailure();
            }
        }

        private void HandleClear()
        {
            StopFighting();
            ReleaseZones();

            int ticketsEarned = config.TicketsPerClear;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierTicketService ticketService))
            {
                ticketService.AddTickets(ticketsEarned);
            }

            PublishClearSummary(ticketsEarned);

            _isActive = false;

            soldierSpawner?.SetSoldiersActive(true);
            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonSessionEndedEvent(true));
        }

        /// <summary>
        /// 기준 스테이지/소요시간/획득 병사 뽑기권을 SoldierRescueDungeonClearedEvent로 발행한다 -
        /// 실제 화면 표시(팝업)는 UI.SoldierRescueDungeonClearPopupUI가 이 이벤트를 구독해 담당한다
        /// (StoneDungeonSessionController.PublishClearSummary와 동일한 형태).
        /// </summary>
        private void PublishClearSummary(int ticketsEarned)
        {
            float elapsed = Mathf.Max(0f, config.TimeLimitSeconds - _remainingTime);
            StageSO referenceStage = config.GetReferenceStage(_stageNumber);
            int chapter = referenceStage != null ? referenceStage.Chapter : 0;
            int stageNumber = referenceStage != null ? referenceStage.StageNumber : 0;

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonClearedEvent(chapter, stageNumber, elapsed, ticketsEarned));
        }

        /// <summary>
        /// 제한시간 종료든 Player 사망이든, 이번 시도를 실패로 처리하고 "구출 실패" 화면을 띄운다.
        /// </summary>
        private void HandleFailure()
        {
            StopFighting();
            ReleaseZones();

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonAttemptFailedEvent());
        }

        private void StopFighting()
        {
            _isFighting = false;

            captureNavigator?.Deactivate();
            sniperAttackSpawner?.Deactivate();
            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Unregister(this);
        }

        private void ReleaseZones()
        {
            if (DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                foreach (WarStructure zone in _activeZones)
                {
                    if (zone != null)
                    {
                        pool.Release(zone.gameObject);
                    }
                }
            }

            _activeZones.Clear();
        }

        private void OnDestroy()
        {
            if (_isFighting)
            {
                StopFighting();
                ReleaseZones();
            }

            if (_isActive)
            {
                _isActive = false;
                soldierSpawner?.SetSoldiersActive(true);
                stageController?.ResumeAfterOverlay();
            }
        }
    }
}
