using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 탐지 범위 내 최근접 생존 적을 주기적으로 찾아 CharacterMover의 추적 대상으로 지정한다.
    /// 사거리 안까지 접근하면 CharacterMover가 스스로 멈추고, 이후 Attacker가 공격을 담당한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class EnemyTracker : MonoBehaviour, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private float retargetInterval = 0.2f;

        private CharacterStatsProvider _statsProvider;
        private CharacterMover _mover;
        private ITargetFilter _targetFilter;
        private Camera _camera;
        private float _elapsed;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _mover = GetComponent<CharacterMover>();
            _targetFilter = GetComponent<ITargetFilter>();
            _camera = Camera.main;
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

            if (_elapsed < retargetInterval)
            {
                return;
            }

            _elapsed = 0f;

            Health target = FindTarget();

            _mover.Target = target != null ? target.transform : null;
            _mover.StoppingDistance = _statsProvider.Stats.AttackRange;

            _targetFilter?.OnTargetAcquired(target);
        }

        /// <summary>
        /// 탐지 범위 내에서 필터가 선호하는 후보 중 최근접을 우선 반환하고,
        /// 선호 후보가 없으면 필터와 무관하게 전체 후보 중 최근접으로 폴백한다.
        /// </summary>
        private Health FindTarget()
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, detectionRange, targetLayerMask);

            Health nearestPreferred = null;
            float nearestPreferredSqrDistance = float.MaxValue;

            Health nearestAny = null;
            float nearestAnySqrDistance = float.MaxValue;

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                if (!IsVisibleToCamera(candidate.transform.position))
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < nearestAnySqrDistance)
                {
                    nearestAnySqrDistance = sqrDistance;
                    nearestAny = health;
                }

                if (_targetFilter != null && !_targetFilter.IsPreferred(health))
                {
                    continue;
                }

                if (sqrDistance < nearestPreferredSqrDistance)
                {
                    nearestPreferredSqrDistance = sqrDistance;
                    nearestPreferred = health;
                }
            }

            return nearestPreferred != null ? nearestPreferred : nearestAny;
        }

        /// <summary>
        /// 화면(카메라 뷰포트) 안에 실제로 들어와 있는지 확인한다. 카메라를 찾을 수 없으면
        /// 화면 판정을 생략하고 항상 보이는 것으로 취급한다(기존 동작으로 안전하게 폴백).
        /// </summary>
        private bool IsVisibleToCamera(Vector3 worldPosition)
        {
            if (_camera == null)
            {
                return true;
            }

            Vector3 viewportPoint = _camera.WorldToViewportPoint(worldPosition);

            return viewportPoint.z > 0f
                && viewportPoint.x >= 0f && viewportPoint.x <= 1f
                && viewportPoint.y >= 0f && viewportPoint.y <= 1f;
        }
    }
}
