using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 접촉(2D 트리거) 중인 대상을 주기적으로 공격한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class ContactAttacker : MonoBehaviour, ITickable
    {
        private CharacterStatsProvider _statsProvider;
        private Health _currentTarget;
        private float _elapsed;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
        }

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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out Health health))
            {
                _currentTarget = health;
                _elapsed = 0f;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out Health health) && health == _currentTarget)
            {
                _currentTarget = null;
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_currentTarget == null || _currentTarget.IsDead)
            {
                return;
            }

            _elapsed += deltaTime;

            RuntimeStats stats = _statsProvider.Stats;

            if (_elapsed < stats.AttackInterval)
            {
                return;
            }

            _elapsed = 0f;
            _currentTarget.TakeDamage(stats.AttackPower);
        }
    }
}
