using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 기마궁수(MountedArcher) 전용 이동 컴포넌트 — RangedKiter처럼 즉시 반대 방향으로 물러나는
    /// 대신, 위협 위치를 중심으로 일정 반지름(orbitRadius)을 유지하며 계속 회전하는 목표 지점을
    /// 쫓는다("말은 180도로 급선회할 수 없다"는 컨셉 — 방향을 홱 트는 대신 원을 그리며 사격).
    /// 이동속도가 빠른 유닛이라는 전제로 설계됐다 — 목표 지점이 계속 앞서가므로, 따라잡을 만큼
    /// 빠르지 않으면 궤도 반경보다 안쪽에 머물게 되어 사실상 서서히 접근하는 것처럼 보인다(의도적
    /// 폴백 — 별도 예외 처리를 두지 않아도 자연스럽게 무너진다).
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class OrbitKiter : MonoBehaviour, ITickable, IMonsterMovementInitializer
    {
        [SerializeField]
        private float retargetInterval = 0.1f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private float orbitRadius = 4f;

        [SerializeField]
        private float orbitAngularSpeedDegrees = 90f;

        [SerializeField]
        private LayerMask allyLayerMask;

        private CharacterMover _mover;
        private CameraFollowService _cameraFollowService;
        private Transform _orbitAnchor;
        private float _elapsed;
        private float _orbitAngleDegrees;

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때의 기본 위협(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
            _orbitAnchor = new GameObject("OrbitKiterAnchor").transform;
        }

        private void OnEnable()
        {
            // 같은 스테이지에 여러 마리가 스폰돼도 전부 같은 위상에서 돌지 않도록 시작 각을 무작위화한다.
            _orbitAngleDegrees = Random.Range(0f, 360f);
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_orbitAnchor != null)
            {
                Destroy(_orbitAnchor.gameObject);
            }
        }

        public void Initialize(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
            Retarget(0f);
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < retargetInterval)
            {
                return;
            }

            float sinceLastRetarget = _elapsed;
            _elapsed = 0f;
            Retarget(sinceLastRetarget);
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

        private void Retarget(float sinceLastRetarget)
        {
            Transform threat = FindThreat();

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            _orbitAngleDegrees += orbitAngularSpeedDegrees * sinceLastRetarget;

            Vector3 offset = Quaternion.Euler(0f, 0f, _orbitAngleDegrees) * Vector3.right * orbitRadius;
            _orbitAnchor.position = threat.position + offset;
            _mover.Target = _orbitAnchor;
            _mover.StoppingDistance = 0f;
        }
    }
}
