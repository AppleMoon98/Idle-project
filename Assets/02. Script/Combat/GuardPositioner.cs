using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 방패벽 전술(Stage.Tactics.ShieldWallFormationGroup)에서, 창병 수보다 방패병이 많이 남았을 때
    /// "여분" 방패병에게 켜지는 이동 컴포넌트 - FormationFollower(보호 대상의 뒤쪽에 숨는다)의
    /// 반대 방향으로, 보호 대상(창병)과 가장 가까운 적을 잇는 선 위, 보호 대상 쪽에서
    /// guardDistance만큼 떨어진 지점(즉 적이 오는 방향)에 자리를 잡는다 - "적이 오는 경로를
    /// 실제로 가로막는다"는 요청을 그대로 구현한 것. 평소(1:1로 충분할 때)엔 비활성 상태로 두고
    /// MonsterTargetSelector가 대신 움직이며, ShieldWallFormationGroup이 재배정을 할 때만
    /// SetProtectedUnit과 함께 켜진다 - 스폰 시점엔 어느 쪽이 필요할지 알 수 없으므로
    /// IMonsterMovementInitializer는 구현하지 않는다(MonsterSpawner가 자동으로 초기화해줄
    /// 대상이 아니라, 재배정 시점에만 명시적으로 켜진다).
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    public sealed class GuardPositioner : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float guardDistance = 1.2f;

        [SerializeField]
        private float retargetInterval = 0.2f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private LayerMask allyLayerMask;

        private CharacterMover _mover;
        private Transform _protectedUnit;
        private Transform _guardAnchor;
        private float _elapsed;

        /// <summary>
        /// 이 방패병이 지켜야 할 대상(창병)을 지정한다. null을 넘기면 위치 계산을 멈추고
        /// 제자리에서 대기한다 - 이 컴포넌트가 비활성화되기 전까지의 과도기적 안전장치일 뿐,
        /// 실제로는 항상 SetProtectedUnit과 enabled=true가 함께 설정된다.
        /// </summary>
        public void SetProtectedUnit(Transform protectedUnit)
        {
            _protectedUnit = protectedUnit;
        }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _guardAnchor = new GameObject("GuardPositionAnchor").transform;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_guardAnchor != null)
            {
                Destroy(_guardAnchor.gameObject);
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
            Retarget();
        }

        private void Retarget()
        {
            if (_protectedUnit == null)
            {
                _mover.Target = null;
                return;
            }

            Health nearest = NearestHealthScan.FindNearest(_protectedUnit.position, detectionRange, allyLayerMask);

            if (nearest == null)
            {
                _mover.Target = null;
                return;
            }

            Vector3 towardThreat = (nearest.transform.position - _protectedUnit.position).normalized;
            _guardAnchor.position = _protectedUnit.position + towardThreat * guardDistance;
            _mover.Target = _guardAnchor;
            _mover.StoppingDistance = 0f;
        }
    }
}
