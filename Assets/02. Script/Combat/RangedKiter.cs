using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 궁병(Archer) 전용 이동 컴포넌트 — MonsterTargetSelector를 대신한다(같은 오브젝트에 둘 다
    /// 붙이지 않음). 위협이 사거리 밖이면 접근해서 사거리 안까지만 다가가고(StoppingDistance=
    /// AttackRange), 위협이 kiteTriggerDistance보다 가까이 오면 KiteRetreatCalculator로 반대
    /// 방향 후퇴 지점을 찾아 물러난다. 실제 사격(Attacker+RangedAttackBehavior)은 이 컴포넌트가
    /// 무엇을 하든 상관없이 자기 사거리 안의 가장 가까운 대상을 독립적으로 스캔해 쏘므로, 여기서는
    /// 순수하게 "어디에 서 있을지"만 담당한다. enablePostAttackLock이 켜져 있으면(기본값) 공격
    /// 직후 postAttackLockFraction × AttackInterval만큼 그 자리에 뿌리박혀 움직이지 않는다(같은
    /// GameObject의 Attacker.AttackPerformed를 구독) — 공격주기 전체를 잠그는 게 아니라 그 절반
    /// 정도만 고정해, 남은 시간에는 정상적으로 접근/후퇴 판단을 계속한다. 사거리와 무관한 순수
    /// "거리 유지" 로직이라 궁병 전용이 아니라 사거리가 긴 다른 근접 유닛(창병 등)에도
    /// 재사용된다(section BZ/EY) — enablePostAttackLock을 끄면 이 고정 없이 매번 재평가만으로
    /// 계속 움직인다(Soldier 쪽 창병 밸런스 조정으로 추가된 옵트아웃, 몬스터 쪽 기본 동작은 그대로).
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class RangedKiter : MonoBehaviour, ITickable, IMonsterMovementInitializer
    {
        [SerializeField]
        private float retargetInterval = 0.2f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private float kiteTriggerDistance = 2f;

        [SerializeField]
        private float kiteStepDistance = 2f;

        [SerializeField]
        private float kiteScreenMargin = 0.05f;

        [SerializeField]
        private LayerMask allyLayerMask;

        [SerializeField]
        private float postAttackLockFraction = 0.5f;

        [SerializeField]
        private bool enablePostAttackLock = true;

        private CharacterMover _mover;
        private CharacterStatsProvider _statsProvider;
        private Attacker _attacker;
        private CameraFollowService _cameraFollowService;
        private Transform _kiteAnchor;
        private float _elapsed;
        private float _lockRemaining;

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때의 기본 위협(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _attacker = GetComponent<Attacker>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
            _kiteAnchor = new GameObject("RangedKiterAnchor").transform;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);

            if (_attacker != null)
            {
                _attacker.AttackPerformed += HandleAttackPerformed;
            }
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);

            if (_attacker != null)
            {
                _attacker.AttackPerformed -= HandleAttackPerformed;
            }

            _lockRemaining = 0f;
        }

        private void HandleAttackPerformed()
        {
            if (!enablePostAttackLock)
            {
                return;
            }

            _lockRemaining = _statsProvider.Stats.AttackInterval * postAttackLockFraction;
            _mover.Target = null;
        }

        private void OnDestroy()
        {
            if (_kiteAnchor != null)
            {
                Destroy(_kiteAnchor.gameObject);
            }
        }

        public void Initialize(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
            Retarget();
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_lockRemaining > 0f)
            {
                _lockRemaining -= deltaTime;
                return;
            }

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
        private Transform FindThreat()
        {
            Health nearest = _cameraFollowService != null
                ? NearestHealthScan.FindNearestInBounds(transform.position, _cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), allyLayerMask)
                : NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);

            return nearest != null ? nearest.transform : PlayerTransform;
        }

        private void Retarget()
        {
            Transform threat = FindThreat();

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            float distance = Vector3.Distance(transform.position, threat.position);

            if (distance < kiteTriggerDistance)
            {
                if (_cameraFollowService != null
                    && KiteRetreatCalculator.TryFindRetreatPoint(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), transform.position, threat.position, kiteStepDistance, kiteScreenMargin, out Vector3 retreatPoint))
                {
                    _kiteAnchor.position = retreatPoint;
                    _mover.Target = _kiteAnchor;
                    _mover.StoppingDistance = 0f;
                }
                else
                {
                    _mover.Target = null;
                }

                return;
            }

            _mover.Target = threat;
            _mover.StoppingDistance = _statsProvider.Stats.AttackRange;
        }
    }
}
