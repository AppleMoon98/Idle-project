using Character.Events;
using Combat;
using Core;
using Stage.Events;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    /// <summary>
    /// 화면 탭 위치로 플레이어를 직접 이동시킨다. Auto/Manual 모드와 무관하게 항상 동작한다 —
    /// 탭하는 동안만 EnemyTracker를 잠깐 끄고(Soldier.SoldierBehaviorController가 원거리
    /// 카이팅에 쓰는 것과 동일한 패턴) 그 자리로 이동시키며, 도착하면 그 시점의 모드가 Auto면
    /// EnemyTracker를 다시 켜 자동 전투를 재개하고 Manual이면 계속 꺼둔 채 정지시킨다(Manual의
    /// "항상 꺼져 있다" 불변식 유지). 공격(Attacker)은 이동 방식과 무관하게 항상 자동으로 동작한다.
    ///
    /// 누르고 있는 시간이 squadRallyHoldSeconds를 넘기면 SquadMoveCommandEvent를 한 번 발행해
    /// 병사들도 같은 위치로 부른다 — 실제 이동/도착 후 재개 로직은 이미 Player/Soldier 모두에
    /// 붙어있는 RallyMoveReceiver가 처리하므로 여기서는 이벤트만 발행한다(이전에는
    /// UI.SquadRallyFlagUI가 깃발 아이콘을 드래그해서 같은 이벤트를 발행했으나, 그 UI를 완전히
    /// 대체한다).
    ///
    /// 앱(에디터 창)이 포커스를 잃는 동안 마우스/터치 버튼을 뗀 경우, OS/입력 시스템이 그
    /// 떼는 이벤트를 앱에 전달하지 못해 Pointer.press.isPressed가 포커스가 돌아온 뒤에도 계속
    /// true로 눌린 채 남아있을 수 있다 - 그러면 실제로는 누른 적 없는데도 집결 홀드 누적이
    /// 이어져 SquadMoveCommandEvent가 제멋대로 발동한다(실사용 중 발견 - "장시간 터치한 적
    /// 없는데 부대가 갑자기 집결 이동함"). 포커스를 잃었다가 되찾는 순간 홀드 누적 상태를
    /// 전부 리셋해, 그 시점에 남아있던 눌림 상태를 신뢰하지 않고 새로 눌러야만 인정한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(EnemyTracker))]
    public sealed class PlayerManualMover : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float squadRallyHoldSeconds = 0.4f;

        [SerializeField]
        private float arrivalDistance = 0.1f;

        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private Transform _tapAnchor;
        private Camera _camera;

        private bool _isMovingToTap;
        private float _pressHeldSeconds;
        private bool _hasTriggeredSquadRally;
        private bool _pressStartedOffUI;
        private bool _wasApplicationFocused = true;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _enemyTracker = GetComponent<EnemyTracker>();
            _camera = Camera.main;

            var anchorGO = new GameObject("PlayerManualMoveAnchor");
            _tapAnchor = anchorGO.transform;
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
            TickerRegistration.Register(this);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService))
            {
                OnControlModeChanged(new PlayerControlModeChangedEvent(controlModeService.CurrentMode));
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_tapAnchor != null)
            {
                Destroy(_tapAnchor.gameObject);
            }
        }

        /// <summary>
        /// Manual로 바뀌면 즉시 EnemyTracker를 끄고(탭 중이었든 아니든 무조건), 탭 이동 중이
        /// 아니라면 CharacterMover.Target도 함께 비운다 - EnemyTracker가 꺼져도 CharacterMover는
        /// 마지막으로 설정된 Target을 향해 계속 이동하므로, 안 비우면 Auto 상태에서 쫓던 몬스터를
        /// 향해 Manual로 전환한 뒤에도 계속 끌려간다. Auto로 바뀌면, 지금 탭 이동 중이 아닐 때만
        /// 즉시 켠다 — 탭 이동 중이라면 도착 시점의 HandleArrival이 알아서 재개하므로 여기서
        /// 미리 켜서 이동을 방해하지 않는다.
        /// </summary>
        private void OnControlModeChanged(PlayerControlModeChangedEvent evt)
        {
            if (evt.Mode == PlayerControlMode.Manual)
            {
                _enemyTracker.enabled = false;

                if (!_isMovingToTap)
                {
                    _mover.Target = null;
                }
            }
            else if (!_isMovingToTap)
            {
                _enemyTracker.enabled = true;
            }
        }

        /// <summary>
        /// 스테이지 전환/반복/사망 후퇴 전부에서 발행된다. StagePositionResetter가 플레이어의
        /// *위치*만 시작 지점으로 되돌릴 뿐 CharacterMover.Target은 손대지 않으므로, 이전
        /// 스테이지에서 남은 탭 이동 목표(_tapAnchor)나 EnemyTracker가 마지막으로 가리키던 몬스터
        /// Transform이 그대로 남아있으면 - 몬스터는 항상 화면 위쪽 스폰 지점에서 다시 태어나므로 -
        /// 위치가 리셋된 직후 플레이어가 그 잔여 Target을 향해 곧장 위로 끌려가는 문제가 실사용
        /// 중(특히 Auto와 달리 EnemyTracker가 스스로 재보정해주지 않는 Manual 모드에서) 발견됐다.
        /// 진행 중이던 탭 이동도 함께 취소하고, Auto 모드면 EnemyTracker를 다시 켠다(탭 이동
        /// 중이었다면 HandleArrival이 켜줬을 것을 여기서 대신 켜주는 것 - 그 도착을 건너뛰고
        /// 강제로 취소했으므로).
        /// </summary>
        private void OnStageChanged(StageChangedEvent evt)
        {
            _isMovingToTap = false;
            _mover.Target = null;
            _mover.StoppingDistance = 0f;

            EnableEnemyTrackerIfAuto();
        }

        void ITickable.Tick(float deltaTime)
        {
            HandleFocusChange();
            HandlePointerInput(deltaTime);
            HandleArrival();
        }

        /// <summary>
        /// 포커스를 잃었다가 되찾는 순간, 그 사이 놓쳤을 수 있는 눌림 해제 이벤트 때문에 남아있는
        /// 눌림 누적 상태를 전부 버린다 - 실제로 새로 누르기 전까지는 집결 홀드가 다시 쌓이지
        /// 않는다.
        /// </summary>
        private void HandleFocusChange()
        {
            bool isFocused = Application.isFocused;

            if (isFocused && !_wasApplicationFocused)
            {
                _pressHeldSeconds = 0f;
                _hasTriggeredSquadRally = false;
                _pressStartedOffUI = false;
            }

            _wasApplicationFocused = isFocused;
        }

        private void HandlePointerInput(float deltaTime)
        {
            if (!Application.isFocused)
            {
                return;
            }

            Pointer pointer = Pointer.current;

            if (pointer == null)
            {
                return;
            }

            // GitHub 이슈 #55 - 두 손가락 핀치 줌의 첫 손가락이 Pointer.current로도 동시에
            // 읽혀 탭 이동/부대 집결이 함께 발동하던 문제. 멀티터치가 진행 중(또는 방금 끝나
            // 손가락이 아직 남아있는 동안)이면 이 프레임의 판정 자체를 건너뛰고, 이미 진행 중이던
            // 후보(홀드 누적/탭 이동)는 즉시 취소한다.
            if (TouchGestureArbiter.ShouldSuppressSingleTouchGestures())
            {
                CancelPendingSingleTouchGestures();
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                Vector2 screenPosition = pointer.position.ReadValue();
                _pressStartedOffUI = !PointerOverUI.IsOverUI(screenPosition);

                if (!_pressStartedOffUI)
                {
                    return;
                }

                BeginTapMove(screenPosition);
                return;
            }

            // 이 프레임의 press가 UI 위에서 시작한 것이면(슬라이더 드래그 등), 손을 떼지 않고
            // squadRallyHoldSeconds를 넘겨도 집결 명령이 발동하면 안 된다 - wasPressedThisFrame
            // 분기에서 이미 UI 위임을 걸러도, 이 홀드 누적 분기는 그 판정과 무관하게 매 프레임
            // 그냥 돌기 때문에 별도로 다시 체크해야 한다.
            if (!_pressStartedOffUI || !pointer.press.isPressed || _hasTriggeredSquadRally)
            {
                return;
            }

            _pressHeldSeconds += deltaTime;

            if (_pressHeldSeconds >= squadRallyHoldSeconds)
            {
                _hasTriggeredSquadRally = true;
                TriggerSquadRally(pointer.position.ReadValue());
            }
        }

        /// <summary>
        /// GitHub 이슈 #55 개선 제안 2번 - 멀티터치(핀치)가 감지되는 순간 진행 중이던 단일 터치
        /// 후보를 전부 취소한다. 아직 이동을 시작 안 한 홀드 누적(부대 집결 후보)은 그냥 리셋하고,
        /// 이미 탭 이동이 시작돼 CharacterMover.Target이 가리키고 있었다면 그 자리에서 멈추고
        /// (정상 도착 시의 HandleArrival과 동일하게) 모드가 Auto면 EnemyTracker를 되돌린다.
        /// </summary>
        private void CancelPendingSingleTouchGestures()
        {
            _pressHeldSeconds = 0f;
            _hasTriggeredSquadRally = false;
            _pressStartedOffUI = false;

            if (_isMovingToTap)
            {
                _isMovingToTap = false;
                _mover.Target = null;
                EnableEnemyTrackerIfAuto();
            }
        }

        private void BeginTapMove(Vector2 screenPosition)
        {
            _pressHeldSeconds = 0f;
            _hasTriggeredSquadRally = false;

            _tapAnchor.position = ScreenToWorld(screenPosition);
            _mover.Target = _tapAnchor;
            _mover.StoppingDistance = 0f;

            _enemyTracker.enabled = false;
            _isMovingToTap = true;
        }

        private void TriggerSquadRally(Vector2 screenPosition)
        {
            GameBootstrapper.Events?.Publish(new SquadMoveCommandEvent(ScreenToWorld(screenPosition)));
        }

        /// <summary>
        /// 탭으로 이동 중일 때만 판정한다. 다른 무언가(집결 명령 등)가 이동 목표를 가로채면
        /// 조용히 추적을 포기한다 — RallyMoveReceiver가 이미 쓰는 것과 동일한 패턴.
        /// </summary>
        private void HandleArrival()
        {
            if (!_isMovingToTap)
            {
                return;
            }

            if (_mover.Target != _tapAnchor)
            {
                _isMovingToTap = false;
                return;
            }

            if (Vector3.Distance(transform.position, _tapAnchor.position) > arrivalDistance)
            {
                return;
            }

            _isMovingToTap = false;
            EnableEnemyTrackerIfAuto();
        }

        private void EnableEnemyTrackerIfAuto()
        {
            bool isAuto = GameBootstrapper.Services != null
                && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService)
                && controlModeService.CurrentMode == PlayerControlMode.Auto;

            if (isAuto)
            {
                _enemyTracker.enabled = true;
            }
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 worldPosition = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            worldPosition.z = 0f;
            return worldPosition;
        }
    }
}
