using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 창병(Spearman) 전용 이동 컴포넌트 — MonsterTargetSelector를 대신한다. 리더(방패 보병 등,
    /// SetLeader로 배정)가 있으면 리더의 "위협 반대쪽"(뒤쪽) 지점을 따라가며 함께 천천히 전진한다.
    /// 리더가 아직 배정되지 않았거나 죽었으면(리더 없음) 평범한 몬스터처럼 위협에게 직접
    /// 접근한다 — 진형 파트너 없이 스폰돼도 완전히 무력화되지는 않는다. 아직 "어느 창병이 어느
    /// 방패 보병 뒤에 서는지"를 스테이지에서 자동으로 짝짓는 병법 스폰 시스템은 없다(나중 작업,
    /// SetLeader는 지금은 외부에서 직접 호출해야 한다) — 이번엔 짝지어졌을 때의 이동 방식 자체만
    /// 구현한다. 창병의 긴 사거리(AttackRange)는 스탯 값만으로 표현되며(리더 뒤에 서 있어도 창이
    /// 리더를 넘어 위협에 닿도록 followDistance + 리더의 AttackRange보다 크게 잡아야 한다), 실제
    /// 공격(Attacker+MeleeAttackBehavior)은 이 컴포넌트와 무관하게 자기 사거리 안의 가장 가까운
    /// 대상을 독립적으로 스캔해 처리한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class FormationFollower : MonoBehaviour, ITickable, IMonsterMovementInitializer
    {
        [SerializeField]
        private float followDistance = 1.5f;

        [SerializeField]
        private float retargetInterval = 0.2f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private LayerMask allyLayerMask;

        private CharacterMover _mover;
        private CharacterStatsProvider _statsProvider;
        private Transform _leader;
        private Transform _followAnchor;
        private float _elapsed;

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때(또는 리더가 없을 때)의 기본 위협(플레이어).
        /// MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        /// <summary>
        /// 이 창병이 뒤따를 리더(방패 보병 등)를 배정한다. null을 넘기면 리더 없음(직접 접근)으로 되돌아간다.
        /// </summary>
        public void SetLeader(Transform leader)
        {
            _leader = leader;
        }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _followAnchor = new GameObject("FormationFollowAnchor").transform;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_followAnchor != null)
            {
                Destroy(_followAnchor.gameObject);
            }
        }

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
            if (_leader == null)
            {
                ApproachDirectly();
                return;
            }

            Health nearest = NearestHealthScan.FindNearest(_leader.position, detectionRange, allyLayerMask);
            Transform threat = nearest != null ? nearest.transform : PlayerTransform;

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            Vector3 behindDirection = (_leader.position - threat.position).normalized;
            _followAnchor.position = _leader.position + behindDirection * followDistance;
            _mover.Target = _followAnchor;
            _mover.StoppingDistance = 0f;
        }

        private void ApproachDirectly()
        {
            Health nearest = NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);
            Transform threat = nearest != null ? nearest.transform : PlayerTransform;

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            _mover.Target = threat;
            _mover.StoppingDistance = _statsProvider.Stats.AttackRange;
        }
    }
}
