using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 궁병(Archer) 전용 이동 컴포넌트 — MonsterTargetSelector를 대신한다(같은 오브젝트에 둘 다
    /// 붙이지 않음). 위협이 사거리 밖이면 접근해서 사거리 안까지만 다가가고(StoppingDistance=
    /// AttackRange), 위협이 kiteTriggerDistance보다 가까이 오면 KiteRetreatCalculator로 반대
    /// 방향 후퇴 지점을 찾아 물러난다. 실제 사격(Attacker+RangedAttackBehavior)은 이 컴포넌트가
    /// 무엇을 하든 상관없이 자기 사거리 안의 가장 가까운 대상을 독립적으로 스캔해 쏘므로, 여기서는
    /// 순수하게 "어디에 서 있을지"만 담당한다.
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

        private CharacterMover _mover;
        private CharacterStatsProvider _statsProvider;
        private Camera _camera;
        private Transform _kiteAnchor;
        private float _elapsed;

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때의 기본 위협(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _camera = Camera.main;
            _kiteAnchor = new GameObject("RangedKiterAnchor").transform;
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
            Health nearest = NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);
            Transform threat = nearest != null ? nearest.transform : PlayerTransform;

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            float distance = Vector3.Distance(transform.position, threat.position);

            if (distance < kiteTriggerDistance)
            {
                if (KiteRetreatCalculator.TryFindRetreatPoint(_camera, transform.position, threat.position, kiteStepDistance, kiteScreenMargin, out Vector3 retreatPoint))
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
