using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Gacha;
using Managers;
using Stage;
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
        /// 스킬 던전을 시작한다. stageNumber는 보스 강함/보상 계산에 쓰인다. 이미 진행 중이거나
        /// (자기 자신) 다른 오버레이가 이미 켜져 있으면(stageController.IsOverlayActive) 무시한다
        /// — GoldDungeonSessionController.Enter와 동일한 이유(던전 중복 진입 방지).
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive) || config == null || config.BossPrefab == null)
            {
                return;
            }

            _isActive = true;
            _stageNumber = stageNumber;

            stageController?.PauseForOverlay();

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

            if (playerTransform != null && playerTransform.TryGetComponent(out Health playerHealth) && playerHealth.IsDead)
            {
                playerHealth.Revive();
            }

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

            GameBootstrapper.Events?.Publish(new SkillDungeonSessionEndedEvent(false));
        }

        private void StartAttempt()
        {
            _remainingTime = config.TimeLimitSeconds;
            _isFighting = true;

            SpawnBoss();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new SkillDungeonAttemptStartedEvent(_stageNumber, _remainingTime));
        }

        private void SpawnBoss()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(config.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.RandomWithinPlayAreaPosition(config.SpawnViewportMargin);
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

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                scrolls.AddScrolls(config.ScrollsPerClearPerStage * _stageNumber);
            }

            _isActive = false;
            _bossInstance = null;

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new SkillDungeonSessionEndedEvent(true));
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
