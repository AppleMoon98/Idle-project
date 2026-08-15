using Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    /// <summary>
    /// CameraZoomControl을 평소엔 화면 왼쪽 밖으로 숨겨두고, 화면 왼쪽 가장자리(EdgeTriggerWidth)에서
    /// 시작한 오른쪽 스와이프로 드러낸다. 열린 상태에서 드로어 자신(어디를 눌러도) 위에서 왼쪽으로
    /// 끌면 다시 숨긴다. 드래그 중엔 손가락을 그대로 따라가며 즉시 반영되고(iOS 제어센터와 동일한
    /// 피드백), 손을 떼는 순간 절반(0.5)을 기준으로 더 가까운 쪽(완전히 열림/닫힘)으로 부드럽게
    /// 스냅한다.
    ///
    /// 닫혀 있는 동안은 이 자리에 실제 화면 밖 오브젝트라 Character.PlayerManualMover의 기존
    /// EventSystem 레이캐스트 검사(IsPointerOverUI)로는 걸러지지 않는다 - PlayerManualMover가
    /// EdgeTriggerWidth를 직접 참조해 "이 폭 안에서 시작한 터치는 탭 이동/집결 홀드 후보에서 아예
    /// 제외"하는 별도 체크를 갖는다(이 컴포넌트가 여는 제스처와 겹치지 않도록). 열려 있을 때는 실제
    /// Image/Slider가 화면에 있으므로 PlayerManualMover의 기존 IsPointerOverUI 체크가 그대로
    /// 걸러준다 - 이 컴포넌트가 별도로 처리할 필요 없음.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CameraZoomDrawerUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        [Range(0.01f, 0.3f)]
        private float edgeTriggerWidthFraction = 0.12f;

        [SerializeField]
        private float snapDuration = 0.15f;

        [SerializeField]
        private float hiddenMargin = 10f;

        /// <summary>
        /// 화면 왼쪽 가장자리에서 이 폭(스크린 픽셀) 안에서 시작한 터치만 열기 제스처 후보로
        /// 인정한다. Character.PlayerManualMover가 탭 이동/집결 홀드를 시작하지 않을 예외 구역을
        /// 판단할 때도 이 값을 그대로 참조한다 - 두 곳에 값을 따로 두면 나중에 어긋난다. 고정
        /// 픽셀값이 아니라 Screen.width의 비율로 계산한다 - Pointer.position은 기기의 실제 화면
        /// 픽셀 기준인데, 고정 픽셀(예: 40)로 두면 해상도가 높은 기기일수록 실제 손가락 터치
        /// 면적 대비 인식 영역이 상대적으로 더 좁아져 스와이프가 탭 이동으로 자꾸 새는 문제가
        /// 있었다(실사용 중 발견).
        /// </summary>
        public float EdgeTriggerWidth => Screen.width * edgeTriggerWidthFraction;

        private RectTransform _rectTransform;
        private Canvas _canvas;

        private float _shownX;
        private float _hiddenX;
        private float _openAmount;
        private bool _isOpen;

        private bool _isDragging;
        private float _dragBaseOpenAmount;
        private float _dragStartLocalX;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            _shownX = _rectTransform.anchoredPosition.x;
            _hiddenX = -(_rectTransform.sizeDelta.x + hiddenMargin);
            _openAmount = 0f;
            ApplyPosition();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            HandlePointerInput();

            if (_isDragging)
            {
                return;
            }

            float target = _isOpen ? 1f : 0f;

            if (Mathf.Approximately(_openAmount, target))
            {
                return;
            }

            _openAmount = Mathf.MoveTowards(_openAmount, target, deltaTime / Mathf.Max(0.01f, snapDuration));
            ApplyPosition();
        }

        private void HandlePointerInput()
        {
            Pointer pointer = Pointer.current;

            if (pointer == null)
            {
                return;
            }

            Vector2 screenPosition = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                bool overDrawer = RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPosition, EventCamera);
                bool inEdgeZone = screenPosition.x <= EdgeTriggerWidth;

                if ((_isOpen && overDrawer) || (!_isOpen && inEdgeZone))
                {
                    BeginDrag(screenPosition);
                }

                return;
            }

            if (!_isDragging)
            {
                return;
            }

            if (!pointer.press.isPressed)
            {
                EndDrag();
                return;
            }

            UpdateDrag(screenPosition);
        }

        private void BeginDrag(Vector2 screenPosition)
        {
            _isDragging = true;
            _dragBaseOpenAmount = _openAmount;
            _dragStartLocalX = ScreenToCanvasLocalX(screenPosition);
        }

        private void UpdateDrag(Vector2 screenPosition)
        {
            float localX = ScreenToCanvasLocalX(screenPosition);
            float deltaX = localX - _dragStartLocalX;
            float travel = _shownX - _hiddenX;
            _openAmount = Mathf.Clamp01(_dragBaseOpenAmount + deltaX / travel);
            ApplyPosition();
        }

        private void EndDrag()
        {
            _isDragging = false;
            _isOpen = _openAmount > 0.5f;
        }

        private void ApplyPosition()
        {
            Vector2 position = _rectTransform.anchoredPosition;
            position.x = Mathf.Lerp(_hiddenX, _shownX, _openAmount);
            _rectTransform.anchoredPosition = position;
        }

        private Camera EventCamera => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private float ScreenToCanvasLocalX(Vector2 screenPosition)
        {
            RectTransform parentRect = _rectTransform.parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, EventCamera, out Vector2 localPoint);
            return localPoint.x;
        }
    }
}
