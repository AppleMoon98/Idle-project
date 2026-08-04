using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Equipment;
using Managers;
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
        /// 강화석 던전을 시작한다. stageNumber는 보스 강함/보상 계산에 쓰인다. 이미 진행 중이면 무시한다.
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || config == null || config.BossPrefab == null)
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

            GameBootstrapper.Events?.Publish(new StoneDungeonSessionEndedEvent(false));
        }

        private void StartAttempt()
        {
            _remainingTime = config.TimeLimitSeconds;
            _isFighting = true;

            SpawnBoss();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            GameBootstrapper.Events?.Publish(new StoneDungeonAttemptStartedEvent(_stageNumber, _remainingTime));
        }

        private void SpawnBoss()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(config.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.RandomOnScreenPosition(config.SpawnViewportMargin);
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

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EnhancementStoneService stones))
            {
                stones.AddStones(config.StonesPerClearPerStage * _stageNumber);
            }

            _isActive = false;
            _bossInstance = null;

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new StoneDungeonSessionEndedEvent(true));
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

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void ReleaseBoss()
        {
            if (_bossInstance != null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
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
