using Character.Events;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 집결 명령(SquadMoveCommandEvent)을 받아 이 유닛을 목표 지점으로 이동시킨다. Auto/Manual
    /// 모드와 무관하게 항상 반응하며, 이동 중에는 componentsToSuspend에 지정된 컴포넌트들
    /// (플레이어=EnemyTracker, 병사=SoldierBehaviorController+EnemyTracker)을 꺼서 이동 제어권을
    /// 가져온다. 도착하면 PlayerControlModeService.CurrentMode를 확인해 Auto면 전부 재활성화하고,
    /// Manual이면 비활성 상태를 유지해 그 자리에 정지시킨다(PlayerManualMover가 이미 지키는
    /// "Auto면 enabled=true, Manual이면 false" 불변식과 동일한 방향으로 수렴하므로 서로 충돌하지
    /// 않는다). 이동 중 다른 무언가가 CharacterMover.Target을 가로채면(예: 플레이어의 탭 이동)
    /// 조용히 추적을 포기한다 — 그 시점부터는 그쪽이 이동 제어권을 정당하게 넘겨받은 것이다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    public sealed class RallyMoveReceiver : MonoBehaviour, ITickable
    {
        [SerializeField]
        private Behaviour[] componentsToSuspend;

        [SerializeField]
        private float arrivalDistance = 0.1f;

        private CharacterMover _mover;
        private Transform _rallyAnchor;
        private bool _isMoving;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _rallyAnchor = new GameObject("RallyAnchor").transform;
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SquadMoveCommandEvent>(OnSquadMoveCommand);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SquadMoveCommandEvent>(OnSquadMoveCommand);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnDestroy()
        {
            if (_rallyAnchor != null)
            {
                Destroy(_rallyAnchor.gameObject);
            }
        }

        private void OnSquadMoveCommand(SquadMoveCommandEvent evt)
        {
            SetComponentsSuspended(true);

            _rallyAnchor.position = evt.WorldPosition;
            _mover.Target = _rallyAnchor;
            _mover.StoppingDistance = 0f;
            _isMoving = true;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isMoving)
            {
                return;
            }

            if (_mover.Target != _rallyAnchor)
            {
                _isMoving = false;
                return;
            }

            if (Vector3.Distance(transform.position, _rallyAnchor.position) > arrivalDistance)
            {
                return;
            }

            _isMoving = false;

            bool isAuto = GameBootstrapper.Services != null
                && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService)
                && controlModeService.CurrentMode == PlayerControlMode.Auto;

            if (isAuto)
            {
                SetComponentsSuspended(false);
            }
        }

        private void SetComponentsSuspended(bool suspended)
        {
            if (componentsToSuspend == null)
            {
                return;
            }

            foreach (Behaviour component in componentsToSuspend)
            {
                if (component != null)
                {
                    component.enabled = !suspended;
                }
            }
        }
    }
}
