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
    /// 생성하고, 기마병만 일정 주기로 계속 리스폰시킨다. 제한시간 안에 구역을 전부 점령하면 클리어
    /// (병사 뽑기 재료 지급), 시간 초과나 플레이어 사망이면 실패(재도전/나가기 대기).
    /// StoneDungeonSessionController와 같은 "StageSO/StageProgression은 건드리지 않는 오버레이"
    /// 골격이지만, 이 던전은 병사 동행이 금지되므로 진입 시 SoldierSpawner를 통해 이미 배치된
    /// 병사를 전부 비활성화하고 종료 시 되돌린다.
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

        private readonly List<WarStructure> _activeZones = new();
        private readonly List<GameObject> _activeCavalry = new();

        private int _stageNumber;
        private float _remainingTime;
        private float _cavalryElapsed;
        private bool _isActive;
        private bool _isFighting;
        private float _lastPublishedProgress;

        /// <summary>
        /// 오버레이가 진행 중인지(전투 중이든 실패 화면 대기 중이든) 여부.
        /// </summary>
        public bool IsActive => _isActive;

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
        /// stageNumber 단계의 입장 조건 — 그 단계의 기준 스테이지(챕터 N의 N-40 스테이지)를 실제로
        /// 클리어한 기록이 있는지 — 를 판정한다. Stone 던전의 IsStageUnlocked와 동일한 이유·형태.
        /// </summary>
        public bool IsStageUnlocked(int stageNumber, out StageSO requiredStage)
        {
            requiredStage = config != null ? config.GetReferenceStage(stageNumber) : null;

            if (requiredStage == null)
            {
                return true;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                return rankService.HasClearedStage(requiredStage);
            }

            return false;
        }

        /// <summary>
        /// 병사 구출 던전을 시작한다. 이미 진행 중이면 무시한다.
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || config == null || config.CavalryPrefab == null || structurePrefab == null)
            {
                return;
            }

            _isActive = true;
            _stageNumber = Mathf.Clamp(stageNumber, 1, MaxStageNumber);

            stageController?.PauseForOverlay();
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

            if (playerTransform != null && playerTransform.TryGetComponent(out Health playerHealth) && playerHealth.IsDead)
            {
                playerHealth.Revive();
            }

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
            _cavalryElapsed = 0f;
            _isFighting = true;
            _lastPublishedProgress = -1f;

            SpawnZones();

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

            var positions = new Vector3[Mathf.Max(config.ZoneCount, 0)];

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 candidate = center;

                for (int attempt = 0; attempt < MaxPlacementAttemptsPerZone; attempt++)
                {
                    float x = Random.Range(center.x - halfExtent.x, center.x + halfExtent.x);
                    float y = Random.Range(center.y - halfExtent.y, center.y + halfExtent.y);
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

            TickCavalrySpawning(deltaTime);
            TickZoneProgress();
        }

        private void TickCavalrySpawning(float deltaTime)
        {
            _cavalryElapsed += deltaTime;

            if (_cavalryElapsed < config.CavalrySpawnInterval)
            {
                return;
            }

            _cavalryElapsed = 0f;
            SpawnCavalry();
        }

        private void SpawnCavalry()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(config.CavalryPrefab, 1, 8);

            Vector3 spawnPosition = DungeonSpawnUtility.RandomWithinPlayAreaPosition(config.SpawnViewportMargin);
            GameObject instance = pool.Get(config.CavalryPrefab, spawnPosition, Quaternion.identity);

            if (instance.TryGetComponent(out StageMonsterScaler scaler))
            {
                scaler.ApplyScale(config.CalculateCavalryStatMultiplier(_stageNumber));
            }

            if (instance.TryGetComponent(out IMonsterMovementInitializer movementInitializer))
            {
                movementInitializer.Initialize(playerTransform);
            }

            _activeCavalry.Add(instance);
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
            // 자연사한(플레이어에게 처치된) 기마병은 Character.PoolReleaseOnDeath가 알아서 풀로
            // 반납하므로, 여기서는 추적만 정리해 세션 종료 시 이중 반납을 막는다.
            _activeCavalry.Remove(evt.Character);

            if (playerTransform != null && evt.Character == playerTransform.gameObject)
            {
                HandleFailure();
            }
        }

        private void HandleClear()
        {
            StopFighting();
            ReleaseZones();
            ReleaseRemainingCavalry();

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierTicketService ticketService))
            {
                ticketService.AddTickets(config.TicketsPerClearPerStage * _stageNumber);
            }

            _isActive = false;

            soldierSpawner?.SetSoldiersActive(true);
            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonSessionEndedEvent(true));
        }

        /// <summary>
        /// 제한시간 종료든 Player 사망이든, 이번 시도를 실패로 처리하고 "구출 실패" 화면을 띄운다.
        /// </summary>
        private void HandleFailure()
        {
            StopFighting();
            ReleaseZones();
            ReleaseRemainingCavalry();

            GameBootstrapper.Events?.Publish(new SoldierRescueDungeonAttemptFailedEvent());
        }

        private void StopFighting()
        {
            _isFighting = false;

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

        /// <summary>
        /// 실패/클리어로 시도가 끝날 때 아직 살아있는(자연사하지 않은) 기마병을 강제로 회수한다.
        /// 자연사한 개체는 OnCharacterDied에서 이미 추적 목록에서 빠졌으므로(Character.PoolReleaseOnDeath가
        /// 알아서 반납) 여기 남은 것들만 반납하면 된다 - Stage.StageProgressTracker.ReleaseRemaining과
        /// 같은 이유(시도가 도중에 강제로 끝나면 죽지 않은 개체는 스스로 반납되지 않는다).
        /// </summary>
        private void ReleaseRemainingCavalry()
        {
            if (DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                foreach (GameObject instance in _activeCavalry)
                {
                    if (instance != null)
                    {
                        pool.Release(instance);
                    }
                }
            }

            _activeCavalry.Clear();
        }

        private void OnDestroy()
        {
            if (_isFighting)
            {
                StopFighting();
                ReleaseZones();
                ReleaseRemainingCavalry();
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
