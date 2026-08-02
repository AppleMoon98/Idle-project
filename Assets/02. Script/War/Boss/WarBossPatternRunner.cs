using Character;
using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace War.Boss
{
    /// <summary>
    /// War 보스의 광역 공격 패턴을 순환 실행한다. 근처 아군(Player/Soldier 레이어) 중 최근접
    /// 위치를 스냅샷해 예고를 표시하고, TelegraphDuration이 지나면 그 자리에 남아있는 대상에게
    /// 데미지를 준다. 판정/타이밍은 이 컴포넌트가 전부 소유하고, WarBossTelegraphIndicator는
    /// 지시받은 대로 그리기만 한다(Combat.Attacker/WeaponSwing과 동일한 분리 철학).
    /// </summary>
    public sealed class WarBossPatternRunner : MonoBehaviour, ITickable, IPoolable
    {
        [SerializeField]
        private WarBossPatternSO[] patterns;

        [SerializeField]
        private float intervalBetweenPatterns = 4f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private LayerMask allyLayerMask;

        [SerializeField]
        private GameObject telegraphIndicatorPrefab;

        [SerializeField]
        private int poolCapacity = 2;

        [SerializeField]
        private int poolMaxSize = 4;

        private PoolManager _pool;
        private int _patternIndex;
        private float _elapsedSinceLastCast;
        private bool _isTelegraphing;
        private float _telegraphElapsed;
        private Vector3 _telegraphPosition;
        private WarBossPatternSO _activePattern;
        private GameObject _activeIndicatorInstance;
        private WarBossTelegraphIndicator _activeIndicatorComponent;

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;

                if (telegraphIndicatorPrefab != null)
                {
                    _pool.EnsurePool(telegraphIndicatorPrefab, poolCapacity, poolMaxSize);
                }
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public void OnSpawned()
        {
            _patternIndex = 0;
            _elapsedSinceLastCast = 0f;
            CancelActivePattern();
        }

        public void OnDespawned()
        {
            CancelActivePattern();
        }

        void ITickable.Tick(float deltaTime)
        {
            if (patterns == null || patterns.Length == 0)
            {
                return;
            }

            if (_isTelegraphing)
            {
                TickTelegraph(deltaTime);
                return;
            }

            _elapsedSinceLastCast += deltaTime;

            if (_elapsedSinceLastCast < intervalBetweenPatterns)
            {
                return;
            }

            TryStartPattern();
        }

        private void TickTelegraph(float deltaTime)
        {
            _telegraphElapsed += deltaTime;

            float progress = _activePattern.TelegraphDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_telegraphElapsed / _activePattern.TelegraphDuration);

            if (_activeIndicatorComponent != null)
            {
                _activeIndicatorComponent.SetProgress01(progress);
            }

            if (_telegraphElapsed >= _activePattern.TelegraphDuration)
            {
                ResolvePattern();
            }
        }

        private void TryStartPattern()
        {
            if (_pool == null || telegraphIndicatorPrefab == null)
            {
                return;
            }

            Transform target = FindNearestAlly();

            if (target == null)
            {
                return;
            }

            _activePattern = patterns[_patternIndex];
            _patternIndex = (_patternIndex + 1) % patterns.Length;

            _telegraphPosition = target.position;
            _telegraphElapsed = 0f;
            _isTelegraphing = true;

            _activeIndicatorInstance = _pool.Get(telegraphIndicatorPrefab, _telegraphPosition, Quaternion.identity);
            _activeIndicatorComponent = _activeIndicatorInstance.GetComponent<WarBossTelegraphIndicator>();
            _activeIndicatorComponent.Show(_telegraphPosition, _activePattern.Radius);
        }

        private void ResolvePattern()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(_telegraphPosition, _activePattern.Radius, allyLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Health health) && !health.IsDead)
                {
                    health.TakeDamage(_activePattern.Damage);
                }
            }

            ReleaseIndicator();
            _activePattern = null;
            _isTelegraphing = false;
            _elapsedSinceLastCast = 0f;
        }

        private void CancelActivePattern()
        {
            ReleaseIndicator();
            _activePattern = null;
            _isTelegraphing = false;
            _telegraphElapsed = 0f;
        }

        private void ReleaseIndicator()
        {
            if (_pool != null && _activeIndicatorInstance != null)
            {
                _pool.Release(_activeIndicatorInstance);
            }

            _activeIndicatorInstance = null;
            _activeIndicatorComponent = null;
        }

        private Transform FindNearestAlly()
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, detectionRange, allyLayerMask);

            Transform nearest = null;
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
                    nearest = candidate.transform;
                }
            }

            return nearest;
        }
    }
}
