using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Equipment;
using Managers;
using Rank;
using Stage;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 강화석 던전(보스전) 한 판의 진행을 관리한다. War 시스템의 보스 프리팹을 화면 안에 스폰해
    /// 실제로 전투(보스가 반격/패턴 공격도 함)하며, 제한시간 안에 처치하면 강화석을 지급하고
    /// 조용히 원래 스테이지로 복귀한다. 제한시간 종료든 Player 사망이든 처치에 실패하면 원래
    /// 스테이지로 자동 복귀하지 않고 "토벌 실패" 상태로 멈춰 재도전/나가기 선택을 기다린다 —
    /// 재도전은 같은 일시정지
    /// 상태를 유지한 채 보스만 다시 스폰하고, 나가기를 눌러야 비로소 StageController가 복귀한다.
    /// GoldDungeonSessionController와 마찬가지로 StageSO/StageProgression 파이프라인은 전혀
    /// 건드리지 않는다.
    /// </summary>
    public sealed class StoneDungeonSessionController : MonoBehaviour, ITickable
    {
        [SerializeField]
        private StoneDungeonConfigSO config;

        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private Transform playerTransform;

        private GameObject _bossInstance;
        private int _stageNumber;
        private float _remainingTime;
        private bool _isActive;
        private bool _isFighting;

        /// <summary>
        /// 오버레이가 진행 중인지(전투 중이든 실패 화면 대기 중이든) 여부.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// UI(StoneDungeonEntryUI)가 스테퍼의 최대 선택 가능 단계를 읽어가기 위한 접근자.
        /// GoldDungeonSessionController.MaxStageNumber와 동일한 이유·동일한 계산: "카탈로그에 존재하는
        /// 콘텐츠 양"이 아니라 "플레이어가 실제로 클리어한 챕터"를 기준으로 삼아, 아직 클리어하지 못한
        /// 챕터의 보스를 기준 삼아 farming하는 것을 막는다. 랭크 서비스가 아직 없으면 최소 1단계는
        /// 항상 선택 가능해야 하므로 1로 대체한다.
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
        /// stageNumber 단계의 입장 조건 — 제거됨(항상 true). requiredStage는 UI 호환을 위해 계속
        /// 채워 반환하지만, 반환값 자체가 항상 true라 UI의 안내 메시지 분기는 더 이상 실행되지 않는다.
        /// </summary>
        public bool IsStageUnlocked(int stageNumber, out StageSO requiredStage)
        {
            requiredStage = config != null ? config.GetReferenceStage(stageNumber) : null;
            return true;
        }

        /// <summary>
        /// 강화석 던전을 시작한다. stageNumber는 보스 강함/보상 계산에 쓰인다. 이미 진행 중이거나
        /// (자기 자신) 다른 오버레이가 이미 켜져 있으면(stageController.IsOverlayActive) 무시한다
        /// — GoldDungeonSessionController.Enter와 동일한 이유(던전 중복 진입 방지). MaxStageNumber
        /// (플레이어가 실제로 클리어한 챕터 기준)로 즉시 정규화해서 저장하므로, UI가 실수로(또는
        /// 스테퍼 상한 설정 전에) 아직 클리어하지 못한 단계를 넘겨도 보스 체력과 강화석 보상이
        /// 항상 같은 유효 단계를 기준으로 계산된다.
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive) || config == null || config.BossPrefab == null)
            {
                return;
            }

            _isActive = true;
            _stageNumber = Mathf.Clamp(stageNumber, 1, MaxStageNumber);

            stageController?.PauseForOverlay($"강화석 던전 {_stageNumber}층");
            stageController?.ResetCombatantsForRetry();
            stageController?.ResetSkillCooldowns();

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

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new StoneDungeonSessionEndedEvent(false));
        }

        private void StartAttempt()
        {
            _remainingTime = config.TimeLimitSeconds;
            _isFighting = true;

            SpawnBoss();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new StoneDungeonAttemptStartedEvent(_stageNumber, _remainingTime, _bossInstance));
        }

        private void SpawnBoss()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(config.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.BossSpawnPosition();
            _bossInstance = pool.Get(config.BossPrefab, spawnPosition, Quaternion.identity);

            if (_bossInstance.TryGetComponent(out StageMonsterScaler scaler))
            {
                scaler.ApplyScale(config.CalculateBossStatMultiplier(_stageNumber));
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

            int stonesEarned = config.StonesPerClearPerStage * _stageNumber;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EnhancementStoneService stones))
            {
                stones.AddStones(stonesEarned);
            }

            PublishClearSummary(stonesEarned);

            _isActive = false;
            _bossInstance = null;

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new StoneDungeonSessionEndedEvent(true));
        }

        /// <summary>
        /// 기준 스테이지/소요시간/획득 강화석을 StoneDungeonClearedEvent로 발행한다 - 실제 화면
        /// 표시(팝업)는 UI.StoneDungeonClearPopupUI가 이 이벤트를 구독해 담당한다
        /// (GoldDungeonSessionController.PublishClearSummary와 동일한 형태).
        /// </summary>
        private void PublishClearSummary(int stonesEarned)
        {
            float elapsed = Mathf.Max(0f, config.TimeLimitSeconds - _remainingTime);
            StageSO referenceStage = config.GetReferenceStage(_stageNumber);
            int chapter = referenceStage != null ? referenceStage.Chapter : 0;
            int stageNumber = referenceStage != null ? referenceStage.StageNumber : 0;

            GameBootstrapper.Events?.Publish(new StoneDungeonClearedEvent(chapter, stageNumber, elapsed, stonesEarned));
        }

        /// <summary>
        /// 제한시간 종료든 Player 사망이든, 이번 시도를 실패로 처리하고 "토벌 실패" 화면을 띄운다.
        /// </summary>
        private void HandleFailure()
        {
            StopFighting();
            ReleaseBoss();

            GameBootstrapper.Events?.Publish(new StoneDungeonAttemptFailedEvent());
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
                stageController?.ResumeAfterOverlay();
            }
        }
    }
}
