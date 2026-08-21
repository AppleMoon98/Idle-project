using System.Collections.Generic;
using Character.Events;
using Combat;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// 화면 왼쪽 가장자리(cameraZoomDrawer.EdgeTriggerWidth 폭)에서 시작한 터치는 탭 이동/집결
    /// 홀드 후보에서 아예 제외한다 - UI.CameraZoomDrawerUI가 그 구역에서 시작한 오른쪽 스와이프로
    /// 카메라 줌 슬라이더를 끌어내는 제스처와 겹치지 않기 위함. 드로어가 열려 있는 동안은 실제
    /// Image/Slider가 화면에 있어 기존 IsPointerOverUI 체크가 자연스럽게 걸러주므로, 이 예외는
    /// 드로어가 닫혀 화면 밖에 있을 때(레이캐스트로 걸러지지 않는 상태)를 위한 것이다.
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

        [SerializeField]
        private UI.CameraZoomDrawerUI cameraZoomDrawer;

        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private Transform _tapAnchor;
        private Camera _camera;

        private bool _isMovingToTap;
        private float _pressHeldSeconds;
        private bool _hasTriggeredSquadRally;
        private bool _pressStartedOffUI;
        private bool _wasApplicationFocused = true;

        private readonly List<RaycastResult> _uiRaycastResults = new();

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
            TickerRegistration.Register(this);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService))
            {
                OnControlModeChanged(new PlayerControlModeChangedEvent(controlModeService.CurrentMode));
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
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
        /// Manual로 바뀌면 즉시 EnemyTracker를 끈다(탭 중이었든 아니든 무조건). Auto로 바뀌면,
        /// 지금 탭 이동 중이 아닐 때만 즉시 켠다 — 탭 이동 중이라면 도착 시점의
        /// HandleArrival이 알아서 재개하므로 여기서 미리 켜서 이동을 방해하지 않는다.
        /// </summary>
        private void OnControlModeChanged(PlayerControlModeChangedEvent evt)
        {
            if (evt.Mode == PlayerControlMode.Manual)
            {
                _enemyTracker.enabled = false;
            }
            else if (!_isMovingToTap)
            {
                _enemyTracker.enabled = true;
            }
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

            if (pointer.press.wasPressedThisFrame)
            {
                Vector2 screenPosition = pointer.position.ReadValue();
                bool inZoomDrawerEdgeZone = cameraZoomDrawer != null && screenPosition.x <= cameraZoomDrawer.EdgeTriggerWidth;
                _pressStartedOffUI = !IsPointerOverUI(screenPosition) && !inZoomDrawerEdgeZone;

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

            bool isAuto = GameBootstrapper.Services != null
                && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService)
                && controlModeService.CurrentMode == PlayerControlMode.Auto;

            if (isAuto)
            {
                _enemyTracker.enabled = true;
            }
        }

        /// <summary>
        /// EventSystem.current.IsPointerOverGameObject()는 터치 입력의 첫 프레임(방금 누른 그
        /// 프레임)에는 아직 EventSystem이 그 터치를 UI 레이캐스트로 처리하기 전이라 신뢰할 수 없다
        /// (항상 false를 반환) - CameraZoomControl의 슬라이더 핸들을 터치했는데도 가끔 탭 이동이
        /// 발동하던 원인이 이것이다. EventSystem의 캐시된 상태를 묻는 대신 지금 이 프레임의
        /// 스크린 좌표로 직접 UI 레이캐스트를 쏴서 즉시 판정하면 이 한 프레임 지연 문제가 없다.
        ///
        /// 레이캐스트 결과 중 순수 UnityEngine.UI.Text(클릭 핸들러 없는 정보성 라벨 — 던전 HUD의
        /// "제한시간" 텍스트 등)만 걸리는 경우는 탭 이동을 막지 않는다 - 새 Text를 만들면
        /// raycastTarget이 기본값 true라 클릭 핸들러가 전혀 없는데도 그 밑을 탭해 이동하는 게
        /// 막히는 버그가 실사용 중 발견됐다. 버튼의 라벨 Text를 눌러도 같은 버튼의 배경 Image가
        /// 레이캐스트 결과에 함께 잡히므로 정상적으로 계속 막히고, 팝업 배경도 보통 Image라
        /// 그대로 막힌다 - Text 하나만 단독으로 잡힐 때만 예외적으로 통과시킨다.
        /// </summary>
        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            PointerEventData eventData = new(EventSystem.current) { position = screenPosition };
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, _uiRaycastResults);

            foreach (RaycastResult result in _uiRaycastResults)
            {
                if (!result.gameObject.TryGetComponent(out UnityEngine.UI.Text _))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 worldPosition = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            worldPosition.z = 0f;
            return worldPosition;
        }
    }
}
