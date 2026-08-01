using Character;
using Core;
using UnityEngine;

namespace War.Objectives
{
    /// <summary>
    /// 수하물 보호 목표. cargoHealth가 protectDuration 동안 생존하면 완료, 그 전에
    /// 사망하면 실패한다. CharacterDiedEvent 구독 대신 매 틱 Health.IsDead를 직접 확인한다
    /// (죽는 순간의 정확한 타이밍보다 "지금 살아있는가"만 필요하므로 더 단순하다).
    /// </summary>
    public sealed class CargoProtectionObjective : MonoBehaviour, ITickable, IWarObjective
    {
        [SerializeField]
        private Health cargoHealth;

        [SerializeField]
        private float protectDuration = 60f;

        private float _elapsed;

        public bool IsCompleted { get; private set; }

        public bool HasFailed { get; private set; }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public void ResetForNewAttempt()
        {
            _elapsed = 0f;
            IsCompleted = false;
            HasFailed = false;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (IsCompleted || HasFailed)
            {
                return;
            }

            if (cargoHealth == null || cargoHealth.IsDead)
            {
                HasFailed = true;
                return;
            }

            _elapsed += deltaTime;

            if (_elapsed >= protectDuration)
            {
                IsCompleted = true;
            }
        }
    }
}
