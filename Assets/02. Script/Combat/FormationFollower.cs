using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 창병(Spearman) 전용 이동 컴포넌트 — MonsterTargetSelector를 대신한다. 리더(방패 보병 등,
    /// SetLeader로 배정)가 있으면 리더의 "위협 반대쪽"(뒤쪽) 지점을 따라가며 함께 천천히 전진한다 —
    /// 단, 이건 아직 전투가 시작되지 않았을 때(자기 사거리 안에 위협이 없을 때)만이다. 자기 사거리
    /// 안에 위협이 들어와 실제로 전투가 시작되면 대형 추종을 완전히 그만두고, 같은 GameObject의
    /// RangedKiter(리더 없는 창병/궁병이 원래 혼자 쓰는 것과 같은 "사거리를 유지하며 물러나는"
    /// 카이팅 컴포넌트, section BZ)에게 넘긴다 — 창은 방패병보다 사거리가 길어(section CN 이후
    /// 재조정) 굳이 붙어 서서 정타를 주고받을 필요가 없으므로, 찌르고 물러나는 게 자연스럽다.
    /// 전투 중에도 계속 리더 뒤 정확한 지점을 쫓아다니면 위협이 조금만 움직여도 따라 흔들리는
    /// 부자연스러운 움직임이 됐던 것도 이 전환으로 함께 해결된다. 이 전환은 되돌리지 않는다(같은
    /// GameObject를 끄는 것으로 완전히 손을 뗀다) — 리더가 재배정되면(Stage.Tactics.
    /// ShieldWallFormationGroup.AssignPrimaryPair) 외부에서 다시 이 컴포넌트를 켜고 SetLeader를
    /// 호출하므로, 여기서 스스로 되돌아갈 필요가 없다. 리더가 아직 배정되지 않았거나 죽었으면(리더
    /// 없음) 평범한 몬스터처럼 위협에게 직접 접근한다 — 진형 파트너 없이 스폰돼도 완전히
    /// 무력화되지는 않는다(다만 이 경로는 실제로는 거의 타지 않는다 — 리더를 잃은 창병은 보통
    /// ShieldWallFormationGroup.ReleaseFollowerAlone이 이 컴포넌트 자체를 끄고 RangedKiter를
    /// 직접 켜기 때문이다). 아직 "어느 창병이 어느 방패 보병 뒤에 서는지"를 스테이지에서 자동으로
    /// 짝짓는 병법 스폰 시스템은 없다(나중 작업, SetLeader는 지금은 외부에서 직접 호출해야 한다) —
    /// 이번엔 짝지어졌을 때의 이동 방식 자체만 구현한다. 실제 공격(Attacker+MeleeAttackBehavior)은
    /// 이 컴포넌트와 무관하게 자기 사거리 안의 가장 가까운 대상을 독립적으로 스캔해 처리한다.
    /// IMonsterMovementInitializer는 구현하지 않는다 - 궁병처럼 RangedKiter(역시 그 인터페이스를
    /// 구현)와 한 프리팹에 같이 있을 수 있는 유닛에서 TryGetComponent&lt;IMonsterMovementInitializer&gt;가
    /// 어느 쪽을 찾을지 모호해지는 것을 피하기 위해서다(GuardPositioner가 같은 이유로 이 인터페이스를
    /// 구현하지 않는 것과 동일한 판단, section BU) - 대신 MonsterSpawner.SpawnPair가 대형 편입
    /// 시점에 Initialize를 직접 호출한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class FormationFollower : MonoBehaviour, ITickable
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
        private CameraFollowService _cameraFollowService;
        private RangedKiter _kiter;
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
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
            _kiter = GetComponent<RangedKiter>();
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

        /// <summary>
        /// 카메라 최광각 고정 범위(줌 배율과 무관) 안의 대상만 위협으로 고려한다 — Combat.CavalryCharge의
        /// 동일한 수정과 같은 이유: detectionRange만 보고 스캔하면 범위 밖(스폰 대기 구역 등)에 막
        /// 나타난 대상까지 "가장 가까운 위협"으로 잡아, 실제로는 화면 안에 있는 더 가까운 대상을
        /// 놔두고 먼 곳으로 향하느라 반응이 늦어 보이는 문제가 있었다. CameraFollowService를
        /// 못 구했을 때만(방어적 폴백) 기존 raw-radius 스캔으로 대체한다.
        /// </summary>
        private Transform FindThreat(Vector3 origin)
        {
            Health nearest = _cameraFollowService != null
                ? NearestHealthScan.FindNearestInBounds(origin, _cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), allyLayerMask)
                : NearestHealthScan.FindNearest(origin, detectionRange, allyLayerMask);

            return nearest != null ? nearest.transform : PlayerTransform;
        }

        private void Retarget()
        {
            if (_leader == null)
            {
                ApproachDirectly();
                return;
            }

            Transform threat = FindThreat(_leader.position);

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            float distanceToThreat = Vector3.Distance(transform.position, threat.position);

            if (distanceToThreat <= _statsProvider.Stats.AttackRange)
            {
                HandOffToKiter(threat);
                return;
            }

            Vector3 behindDirection = (_leader.position - threat.position).normalized;
            _followAnchor.position = _leader.position + behindDirection * followDistance;
            _mover.Target = _followAnchor;
            _mover.StoppingDistance = 0f;
        }

        /// <summary>
        /// 대형 추종(리더 뒤 지점 쫓기)을 그만두고 RangedKiter에게 완전히 넘긴다(자신은 꺼짐,
        /// 되돌아가지 않음 — 클래스 doc 참고). RangedKiter가 없는 예외적인 경우에만 예전처럼
        /// 위협에게 직접 붙어 사거리에서 정지하는 평범한 접근으로 대체한다.
        /// </summary>
        private void HandOffToKiter(Transform threat)
        {
            if (_kiter != null)
            {
                _kiter.Initialize(PlayerTransform);
                _kiter.enabled = true;
                enabled = false;
                return;
            }

            _mover.Target = threat;
            _mover.StoppingDistance = _statsProvider.Stats.AttackRange;
        }

        private void ApproachDirectly()
        {
            Transform threat = FindThreat(transform.position);

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            HandOffToKiter(threat);
        }
    }
}
