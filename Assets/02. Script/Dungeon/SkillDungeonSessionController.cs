using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Gacha;
using Managers;
using Stage;
using UI.Events;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 스킬 던전(보스전) 한 판의 진행을 관리한다. StoneDungeonSessionController와 완전히 동일한
    /// 형태 — 화면 안에 보스를 스폰해 실제로 전투하며, 제한시간 안에 처치하면 스킬 주문서를
    /// 지급하고 조용히 원래 스테이지로 복귀한다. 제한시간 종료든 Player 사망이든 처치에 실패하면
    /// 원래 스테이지로 자동 복귀하지 않고 "토벌 실패" 상태로 멈춰 재도전/나가기 선택을 기다린다.
    /// StageSO/StageProgression 파이프라인은 전혀 건드리지 않는다.
    /// </summary>
    public sealed class SkillDungeonSessionController : MonoBehaviour, ITickable
    {
        [SerializeField]
        private SkillDungeonConfigSO config;

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
        /// 실제로 전투가 진행 중인지(실패 화면 대기 중이 아닌지) 여부(GitHub 이슈 #25 -
        /// UI.BackInputRouter가 뒤로가기 시 "실패 대기 상태에서만 나가기" 정책을 판단하는 데 쓴다).
        /// SoldierRescueDungeonSessionController.IsFighting과 동일한 이유·동일한 계산.
        /// </summary>
        public bool IsFighting => _isFighting;

        /// <summary>
        /// 스킬 던전을 시작한다. stageNumber는 보스 강함/보상 계산에 쓰인다. 이미 진행 중이거나
        /// (자기 자신) 다른 오버레이가 이미 켜져 있으면(stageController.IsOverlayActive) 무시하고
        /// 토스트로 안내한다 — GoldDungeonSessionController.Enter와 동일한 이유(던전 중복 진입 방지).
        /// </summary>
        /// <summary>
        /// 보스 스폰을 먼저 시도해 성공했을 때만 상태를 커밋한다(GitHub 이슈 #20) — 실패하면
        /// PauseForOverlay조차 호출하지 않아 롤백할 상태가 없다.
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive))
            {
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("이미 던전에 입장중입니다."));
                return;
            }

            if (config == null || config.BossPrefab == null)
            {
                return;
            }

            _stageNumber = stageNumber;

            if (!TrySpawnBoss(out GameObject bossInstance))
            {
                PublishSpawnFailureToast();
                return;
            }

            _isActive = true;
            _bossInstance = bossInstance;

            stageController?.PauseForOverlay($"스킬 던전 {_stageNumber}층");
            stageController?.ResetCombatantsForRetry();
            stageController?.ResetSkillCooldowns();

            BeginFighting();
        }

        /// <summary>
        /// 토벌 실패 후 재도전한다. 진행 중이 아니거나 아직 전투 중이면 무시한다. 보스 재스폰이
        /// 실패하면 기존 "토벌 실패" 화면 상태를 그대로 유지한다(GitHub 이슈 #20).
        /// </summary>
        public void Retry()
        {
            if (!_isActive || _isFighting)
            {
                return;
            }

            if (!TrySpawnBoss(out GameObject bossInstance))
            {
                PublishSpawnFailureToast();
                return;
            }

            _bossInstance = bossInstance;

            stageController?.ResetCombatantsForRetry();

            BeginFighting();
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

            GameBootstrapper.Events?.Publish(new SkillDungeonSessionEndedEvent(false));
        }

        /// <summary>
        /// 보스 스폰이 이미 성공한 뒤(_bossInstance가 세팅된 뒤) 전투 시작 북키핑만 한다.
        /// </summary>
        private void BeginFighting()
        {
            _remainingTime = config.TimeLimitSeconds;
            _isFighting = true;

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new SkillDungeonAttemptStartedEvent(_stageNumber, _remainingTime, _bossInstance));
        }

        /// <summary>
        /// 성공하면 true와 함께 instance를 채운다. PoolManager를 못 구하면 아무것도 안 건드리고
        /// false만 반환한다(GitHub 이슈 #20).
        /// </summary>
        private bool TrySpawnBoss(out GameObject instance)
        {
            instance = null;

            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return false;
            }

            pool.EnsurePool(config.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.BossSpawnPosition();
            instance = pool.Get(config.BossPrefab, spawnPosition, Quaternion.identity);

            if (instance == null)
            {
                return false;
            }

            if (instance.TryGetComponent(out StageMonsterScaler scaler))
            {
                scaler.ApplyScale(config.CalculateBossStatMultiplier(_stageNumber));
            }

            return true;
        }

        private static void PublishSpawnFailureToast()
        {
            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("전투 대상을 생성하지 못했습니다. 잠시 후 다시 시도해주세요."));
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

            int scrollsEarned = config.ScrollsPerClearPerStage * _stageNumber;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                scrolls.AddScrolls(scrollsEarned);
            }

            PublishClearSummary(scrollsEarned);

            _isActive = false;
            _bossInstance = null;

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new SkillDungeonSessionEndedEvent(true));
        }

        /// <summary>
        /// 단계/소요시간/획득 주문서를 SkillDungeonClearedEvent로 발행한다 - 실제 화면 표시(팝업)는
        /// UI.SkillDungeonClearPopupUI가 이 이벤트를 구독해 담당한다. StoneDungeonConfigSO와 달리
        /// 챕터 기준 스테이지 개념이 없어(section BI) 단계 번호만 담는다.
        /// </summary>
        private void PublishClearSummary(int scrollsEarned)
        {
            float elapsed = Mathf.Max(0f, config.TimeLimitSeconds - _remainingTime);

            GameBootstrapper.Events?.Publish(new SkillDungeonClearedEvent(_stageNumber, elapsed, scrollsEarned));
        }

        /// <summary>
        /// 제한시간 종료든 Player 사망이든, 이번 시도를 실패로 처리하고 "토벌 실패" 화면을 띄운다.
        /// </summary>
        private void HandleFailure()
        {
            StopFighting();
            ReleaseBoss();

            GameBootstrapper.Events?.Publish(new SkillDungeonAttemptFailedEvent());
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
