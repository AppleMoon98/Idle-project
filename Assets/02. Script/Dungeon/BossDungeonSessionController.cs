using System.Collections.Generic;
using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Managers;
using Rank;
using Rank.Boss;
using Soldier;
using Stage;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 보스 던전(승급전 보스 재도전) 한 판의 진행을 관리한다. Rank.RankSO.BossPrefab 중 실제
    /// 승급전 보스(Rank.Boss.PromotionBossController 보유)이면서 플레이어가 이미 그 랭크 이상인
    /// 것만 선택 가능한 목록으로 노출한다(GetAvailableBosses) — 그래서 별도의 "단계" 개념이 없다.
    /// 선택된 보스를 승급전보다 더 강하게(config.ExtraStrengthMultiplier) 스폰해 제한시간 안에
    /// 처치하면 보스 토벌 증표를 지급한다. StoneDungeonSessionController와 동일한 오버레이 골격
    /// (StageController.PauseForOverlay/ResumeAfterOverlay) — StageSO/StageProgression 파이프라인은
    /// 전혀 건드리지 않는다. Soldier.SoldierRescueDungeonSessionController와 마찬가지로 병사 동행이
    /// 금지되므로, 진입 시 SoldierSpawner를 통해 이미 배치된 병사를 전부 비활성화하고 종료 시 되돌린다.
    /// </summary>
    public sealed class BossDungeonSessionController : MonoBehaviour, ITickable
    {
        [SerializeField]
        private BossDungeonConfigSO config;

        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private SoldierSpawner soldierSpawner;

        private GameObject _bossInstance;
        private RankSO _selectedRank;
        private float _remainingTime;
        private bool _isActive;
        private bool _isFighting;

        /// <summary>
        /// 오버레이가 진행 중인지(전투 중이든 실패 화면 대기 중이든) 여부.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// 지금 선택 가능한 보스가 하나라도 있는지 — UI.BossDungeonRowUI가 DungeonPopup의 입장
        /// 버튼 잠금 해제 여부를 여기 하나로 판정한다(별도로 "병사 랭크 이상"을 하드코딩하지 않음 —
        /// 실제로 승급전 보스를 가진 랭크에 도달했는지만 본다).
        /// </summary>
        public bool HasAnyBossAvailable => GetAvailableBosses().Count > 0;

        /// <summary>
        /// 선택 가능한 승급전 보스 랭크 목록. config.RankCatalog를 순서대로 훑어, BossPrefab이
        /// 실제 승급전 보스(PromotionBossController 보유 — 아직 콘텐츠가 없는 랭크는 BossPrefab이
        /// 있어도 placeholder Monster_Boss.prefab이라 PromotionBossController가 없다)이고,
        /// 플레이어가 이미 그 랭크 이상(RankService.IsAtLeast)인 것만 담는다. 랭크 순서 그대로라
        /// 새 승급전 보스가 추가돼도 코드 변경 없이 목록이 늘어난다.
        /// </summary>
        public IReadOnlyList<RankSO> GetAvailableBosses()
        {
            var result = new List<RankSO>();

            if (config == null || config.RankCatalog == null || config.RankCatalog.Ranks == null)
            {
                return result;
            }

            RankService rankService = null;
            GameBootstrapper.Services?.TryGet(out rankService);

            if (rankService == null)
            {
                return result;
            }

            foreach (RankSO rank in config.RankCatalog.Ranks)
            {
                if (rank == null || rank.BossPrefab == null)
                {
                    continue;
                }

                if (!rank.BossPrefab.TryGetComponent(out PromotionBossController _))
                {
                    continue;
                }

                if (rankService.IsAtLeast(rank))
                {
                    result.Add(rank);
                }
            }

            return result;
        }

        /// <summary>
        /// selectedRank의 승급전 보스로 보스 던전을 시작한다. selectedRank가 선택 가능한 목록에
        /// 없으면(잠긴 랭크를 억지로 넘기는 등) 무시한다. 이미 진행 중이거나(자기 자신) 다른
        /// 오버레이가 이미 켜져 있으면(stageController.IsOverlayActive) 무시한다 —
        /// GoldDungeonSessionController.Enter와 동일한 이유(던전 중복 진입 방지).
        /// </summary>
        public void Enter(RankSO selectedRank)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive) || config == null || selectedRank == null)
            {
                return;
            }

            bool isAvailable = false;

            foreach (RankSO rank in GetAvailableBosses())
            {
                if (rank == selectedRank)
                {
                    isAvailable = true;
                    break;
                }
            }

            if (!isAvailable)
            {
                return;
            }

            _isActive = true;
            _selectedRank = selectedRank;

            stageController?.PauseForOverlay($"보스 토벌 - {selectedRank.DisplayName}");
            soldierSpawner?.SetSoldiersActive(false);

            StartAttempt();
        }

        /// <summary>
        /// 토벌 실패 후 재도전한다. 진행 중이 아니거나 아직 전투 중이면 무시한다.
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
        /// 토벌 실패 후 나가기 — 원래 스테이지로 복귀한다. 전투 중이 아닐 때만 유효하다.
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

            GameBootstrapper.Events?.Publish(new BossDungeonSessionEndedEvent(false));
        }

        private void StartAttempt()
        {
            _remainingTime = config.TimeLimitSeconds;
            _isFighting = true;

            SpawnBoss();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new BossDungeonAttemptStartedEvent(_selectedRank, _remainingTime, _bossInstance));
        }

        private void SpawnBoss()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(_selectedRank.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.RandomWithinPlayAreaPosition(config.SpawnViewportMargin);
            _bossInstance = pool.Get(_selectedRank.BossPrefab, spawnPosition, Quaternion.identity);

            if (_bossInstance.TryGetComponent(out StageMonsterScaler scaler))
            {
                scaler.ApplyScale(config.ExtraStrengthMultiplier);
            }
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
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character == _bossInstance)
            {
                HandleClear();
                return;
            }

            if (playerTransform != null && evt.Character == playerTransform.gameObject)
            {
                HandleFailure();
            }
        }

        private void HandleClear()
        {
            StopFighting();

            int tokensEarned = config.TokensPerClear;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out BossTokenService tokenService))
            {
                tokenService.AddTokens(tokensEarned);
            }

            PublishClearSummary(tokensEarned);

            _isActive = false;
            _bossInstance = null;

            soldierSpawner?.SetSoldiersActive(true);
            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new BossDungeonSessionEndedEvent(true));
        }

        /// <summary>
        /// 처치한 보스 이름/소요시간/획득 증표를 BossDungeonClearedEvent로 발행한다 - 실제 화면
        /// 표시(팝업)는 UI.BossDungeonClearPopupUI가 이 이벤트를 구독해 담당한다
        /// (StoneDungeonSessionController.PublishClearSummary와 동일한 형태).
        /// </summary>
        private void PublishClearSummary(int tokensEarned)
        {
            float elapsed = Mathf.Max(0f, config.TimeLimitSeconds - _remainingTime);
            string bossDisplayName = _selectedRank != null ? _selectedRank.DisplayName : "";

            GameBootstrapper.Events?.Publish(new BossDungeonClearedEvent(bossDisplayName, elapsed, tokensEarned));
        }

        /// <summary>
        /// 제한시간 종료든 Player 사망이든, 이번 시도를 실패로 처리하고 "토벌 실패" 화면을 띄운다.
        /// </summary>
        private void HandleFailure()
        {
            StopFighting();
            ReleaseBoss();

            GameBootstrapper.Events?.Publish(new BossDungeonAttemptFailedEvent());
        }

        private void StopFighting()
        {
            _isFighting = false;

            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Unregister(this);
        }

        private void ReleaseBoss()
        {
            if (_bossInstance != null && DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                pool.Release(_bossInstance);
            }

            _bossInstance = null;
        }

        private void OnDestroy()
        {
            if (_isFighting)
            {
                StopFighting();
                ReleaseBoss();
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
