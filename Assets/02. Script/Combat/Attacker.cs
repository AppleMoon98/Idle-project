using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 사거리 내 최근접 타겟을 주기적으로 자동 공격한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class Attacker : MonoBehaviour, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        private CharacterStatsProvider _statsProvider;
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

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            RuntimeStats stats = _statsProvider.Stats;

            if (_elapsed < stats.AttackInterval)
            {
                return;
            }

            _elapsed = 0f;

            Health target = FindNearestTarget(stats.AttackRange);

            if (target != null)
            {
                target.TakeDamage(stats.AttackPower);
            }
        }

        private Health FindNearestTarget(float range)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, range, targetLayerMask);

            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            }

            return nearest;
        }
    }
}
