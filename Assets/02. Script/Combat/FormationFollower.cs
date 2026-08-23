using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 방패벽 전술의 2열 이하 유닛(창병/궁병 등, SetLeader로 리더인 방패병을 배정) 전용 이동
    /// 컴포넌트. 리더의 "위치"를 따라가는 게 아니라, 리더가 지금 실제로 쫓고 있는 대상
    /// (Character.CharacterMover.Target)을 그대로 자기 목표로 삼아 같은 곳으로 이동한다 — 리더가
    /// 아직 아무 대상도 못 찾았으면(Target == null) 같이 대기한다. 속도는 이 컴포넌트가 직접
    /// 맞추지 않는다 — 리더와 팔로워가 항상 같은 부대(Soldier.SquadMovementSyncService의
    /// 슬롯/부대 인덱스 기준)이고 둘 다 "행군 중"으로 표시되는 한, 그 서비스가 부대 최저속으로
    /// 자동 동기화하므로 여기서 따로 계산할 필요가 없다.
    ///
    /// 자기 사거리 안에 그 목표가 들어오면(실제 전투 시작) 대형 추종을 그만두고, 같은 GameObject의
    /// RangedKiter(리더 없는 창병/궁병이 원래 혼자 쓰는 것과 같은 "사거리를 유지하며 물러나는"
    /// 카이팅 컴포넌트, section BZ)에게 넘긴다 — 창은 방패병보다 사거리가 길어 굳이 붙어 서서
    /// 정타를 주고받을 필요가 없으므로, 찌르고 물러나는 게 자연스럽다. 이 전환은 되돌리지 않는다
    /// (같은 GameObject를 끄는 것으로 완전히 손을 뗀다) — 리더가 재배정되면 외부(Soldier.
    /// SquadShieldWallCoordinator.Pair)에서 다시 이 컴포넌트를 켜고 SetLeader를 호출한다.
    ///
    /// 궁병(Soldier.SoldierBehaviorController가 FormationFollower+Combat.RangedAttackBehavior를
    /// 함께 가진 것으로 판별)은 이 클래스의 전투 핸드오프를 타지 않는다 — 궁병의 "원래 방식"은
    /// TickRangedKiting()이므로, SoldierBehaviorController가 자기 사거리 판정으로 직접 이 컴포넌트를
    /// 끄고 그쪽으로 넘긴다(이 클래스 안에서 사거리 체크를 하더라도, 어차피 곧 꺼지므로 무해하다).
    ///
    /// 리더가 아직 배정되지 않았거나 죽었으면(리더 없음) 평범한 몬스터처럼 위협에게 직접 접근한다 —
    /// 진형 파트너 없이 스폰돼도 완전히 무력화되지는 않는다(다만 이 경로는 실제로는 거의 타지
    /// 않는다 — 리더를 잃은 창병은 보통 ShieldWallFormationGroup.ReleaseFollowerAlone이 이 컴포넌트
    /// 자체를 끄고 RangedKiter를 직접 켜기 때문이다). 실제 공격(Attacker+MeleeAttackBehavior 등)은
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
        private CharacterMover _leaderMover;
        private float _elapsed;

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때(또는 리더가 없을 때)의 기본 위협(플레이어).
        /// MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        /// <summary>
        /// 이 유닛이 뒤따를 리더(방패 보병 등)를 배정한다. null을 넘기면 리더 없음(직접 접근)으로
        /// 되돌아간다.
        /// </summary>
        public void SetLeader(Transform leader)
        {
            _leader = leader;
            _leaderMover = leader != null ? leader.GetComponent<CharacterMover>() : null;
        }

        /// <summary>
        /// 리더가 배정돼 있는지. Soldier.SoldierBehaviorController가 궁병(FormationFollower+
        /// RangedAttackBehavior를 함께 갖는 경우)의 대형 추종 여부를 판단하는 데 쓴다.
        /// </summary>
        public bool HasLeader => _leader != null;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
            _kiter = GetComponent<RangedKiter>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
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
        /// 카메라 최광각 고정 범위(줌 배율과 무관) 안의 대상만 위협으로 고려한다 — Combat.BearCharge의
        /// 동일한 수정과 같은 이유. CameraFollowService를 못 구했을 때만(방어적 폴백) 기존
        /// raw-radius 스캔으로 대체한다. 리더 없이(ApproachDirectly) 혼자 위협을 찾을 때만 쓴다.
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
            if (_leader == null || _leaderMover == null)
            {
                ApproachDirectly();
                return;
            }

            Transform target = _leaderMover.Target;

            if (target == null)
            {
                _mover.Target = null;
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            if (distanceToTarget <= _statsProvider.Stats.AttackRange)
            {
                HandOffToKiter(target);
                return;
            }

            _mover.Target = target;
            _mover.StoppingDistance = 0f;
        }

        /// <summary>
        /// 대형 추종을 그만두고 RangedKiter에게 완전히 넘긴다(자신은 꺼짐, 되돌아가지 않음 —
        /// 클래스 doc 참고). RangedKiter가 없는 예외적인 경우에만 예전처럼 위협에게 직접 붙어
        /// 사거리에서 정지하는 평범한 접근으로 대체한다.
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
