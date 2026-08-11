using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 최광각 고정 범위(줌 배율과 무관, Services.CameraFollowService 기준) 안의 최근접 생존 적을
    /// 주기적으로 찾아 CharacterMover의 추적 대상으로 지정한다. 사거리 안까지 접근하면
    /// CharacterMover가 스스로 멈추고, 이후 Attacker가 공격을 담당한다. 별도의 거리 기반 "탐지
    /// 범위" 스탯은 없다 — 예전에는 탐지 반경(detectionRange)과 화면 안 여부를 별도 조건으로
    /// AND 판정했는데, 그러면 화면(=최광각 범위) 안에 들어온 적이라도 탐지 반경보다 멀면 인지를
    /// 못하는 모순이 있었다(병사가 배치 지점에서 멀리 떨어진 채 대기 중이면 특히 두드러졌다).
    /// 지금은 최광각 범위 자체가 유일한 판정 기준이다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class EnemyTracker : MonoBehaviour, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private float retargetInterval = 0.2f;

        private CharacterStatsProvider _statsProvider;
        private CharacterMover _mover;
        private ITargetFilter _targetFilter;
        private CameraFollowService _cameraFollowService;
        private float _elapsed;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _mover = GetComponent<CharacterMover>();
            _targetFilter = GetComponent<ITargetFilter>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
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
        /// 최광각 고정 범위 안에서 필터가 선호하는 후보 중 최근접을 우선 반환하고,
        /// 선호 후보가 없으면 필터와 무관하게 전체 후보 중 최근접으로 폴백한다.
        /// CameraFollowService를 못 구했으면(방어적 폴백) 판정 기준 범위 자체가 없으므로 null.
        /// </summary>
        private Health FindTarget()
        {
            if (_cameraFollowService == null)
            {
                return null;
            }

            Health nearestPreferred = null;
            float nearestPreferredSqrDistance = float.MaxValue;

            Health nearestAny = null;
            float nearestAnySqrDistance = float.MaxValue;

            NearestHealthScan.ForEachAliveCandidateInBounds(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), targetLayerMask, (candidate, health) =>
            {
                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < nearestAnySqrDistance)
                {
                    nearestAnySqrDistance = sqrDistance;
                    nearestAny = health;
                }

                if (_targetFilter != null && !_targetFilter.IsPreferred(health))
                {
                    return;
                }

                if (sqrDistance < nearestPreferredSqrDistance)
                {
                    nearestPreferredSqrDistance = sqrDistance;
                    nearestPreferred = health;
                }
            });

            return nearestPreferred != null ? nearestPreferred : nearestAny;
        }
    }
}
