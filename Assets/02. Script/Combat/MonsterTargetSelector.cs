using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 몬스터의 이동 대상을 주기적으로 재평가한다. allyLayerMask(플레이어+병사) 안에서
    /// 살아있는 최근접 대상을 찾아 그리로 이동한다. 탐지 범위 안에 아무도 없으면 플레이어를
    /// 기본값으로 삼는다(항상 갈 곳이 있도록). EnemyTracker(플레이어/병사 쪽)와 동일하게
    /// StoppingDistance를 Stats.AttackRange로 설정해, 타겟 위치에 완전히 겹치지 않고
    /// 사거리 안에서 멈추게 한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class MonsterTargetSelector : MonoBehaviour, ITickable, IMonsterMovementInitializer
    {
        [SerializeField]
        private float retargetInterval = 0.2f;

        [SerializeField]
        private float detectionRange = 30f;

        [SerializeField]
        private LayerMask allyLayerMask;

        private CharacterMover _mover;
        private CharacterStatsProvider _statsProvider;
        private CameraFollowService _cameraFollowService;
        private float _elapsed;

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때의 기본 이동 대상(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
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

        /// <summary>
        /// 플레이어 Transform을 주입하고 즉시 한 번 재평가한다. 스폰 직후 MonsterSpawner가 호출한다.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
            Retarget();
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < retargetInterval)
            {
                return;
            }

            _elapsed = 0f;
            Retarget();
        }

        private void Retarget()
        {
            _mover.Target = ChooseTarget();
            _mover.StoppingDistance = _statsProvider.Stats.AttackRange;
        }

        /// <summary>
        /// 카메라 최광각 고정 범위(줌 배율과 무관) 안의 대상만 이동 목표로 고려한다 — Combat.BearCharge와
        /// 같은 이유로, 범위 밖 대상까지 detectionRange만 보고 잡으면 화면 밖의 먼 대상에 반응이
        /// 늦어 보이는 일관성 문제가 생긴다. CameraFollowService를 못 구했을 때만(방어적 폴백)
        /// 기존 raw-radius 스캔으로 대체한다.
        /// </summary>
        private Transform ChooseTarget()
        {
            Health nearest = _cameraFollowService != null
                ? NearestHealthScan.FindNearestInBounds(transform.position, _cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), allyLayerMask)
                : NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);

            return nearest != null ? nearest.transform : PlayerTransform;
        }
    }
}
