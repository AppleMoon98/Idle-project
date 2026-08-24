using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// CameraZoomControl을 평소엔 화면 왼쪽 밖으로 숨겨두고, 전용 토글 버튼(ZoomToggleButton)을
    /// 누를 때마다 꺼내고/넣는다. 예전엔 화면 왼쪽 가장자리에서 시작한 오른쪽 스와이프로 열고,
    /// 열린 드로어를 왼쪽으로 끌어 닫는 제스처 기반이었으나(EdgeTriggerWidth로 판정) 버튼 클릭
    /// 한 번으로 대체됐다 - 꺼내고 넣는 슬라이드 애니메이션(부드러운 스냅) 자체는 그대로 유지한다.
    /// ZoomToggleButton(toggleButtonRect)이 연결돼 있으면, 드로어와 정확히 같은 _openAmount로
    /// 그 버튼의 x좌표도 함께 보간한다 - 별도 트윈이 아니라 같은 값을 공유해서 움직이므로 항상
    /// 드로어와 정확히 같은 속도로, 같은 타이밍에 도착한다(버튼이 드로어를 뒤따라오는 손잡이처럼
    /// 보이도록). toggleButtonLabel이 연결돼 있으면 버튼 라벨도 함께 갱신한다 - 애니메이션이
    /// 끝나길 기다리지 않고 Toggle() 호출 즉시(목표 상태 기준으로) "▶"(닫힘, 화면 밖)/"◀"(열림,
    /// 화면 안)로 바뀐다 - 버튼을 누른 순간 바로 "이제 이 방향으로 움직인다"는 피드백을 준다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CameraZoomDrawerUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float snapDuration = 0.15f;

        [SerializeField]
        private float hiddenMargin = 10f;

        [SerializeField]
        private RectTransform toggleButtonRect;

        [SerializeField]
        private float toggleButtonHiddenX = -10f;

        [SerializeField]
        private float toggleButtonShownX = 110f;

        [SerializeField]
        private Text toggleButtonLabel;

        private const string ClosedLabel = "▶";
        private const string OpenLabel = "◀";

        private RectTransform _rectTransform;

        private float _shownX;
        private float _hiddenX;
        private float _openAmount;
        private bool _isOpen;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            _shownX = _rectTransform.anchoredPosition.x;
            _hiddenX = -(_rectTransform.sizeDelta.x + hiddenMargin);
            _openAmount = 0f;
            ApplyPosition();
            UpdateLabel();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// ZoomToggleButton의 onClick이 호출한다. 열려 있으면 닫고, 닫혀 있으면 연다.
        /// </summary>
        public void Toggle()
        {
            _isOpen = !_isOpen;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (toggleButtonLabel != null)
            {
                toggleButtonLabel.text = _isOpen ? OpenLabel : ClosedLabel;
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            float target = _isOpen ? 1f : 0f;

            if (Mathf.Approximately(_openAmount, target))
            {
                return;
            }

            _openAmount = Mathf.MoveTowards(_openAmount, target, deltaTime / Mathf.Max(0.01f, snapDuration));
            ApplyPosition();
        }

        private void ApplyPosition()
        {
            Vector2 position = _rectTransform.anchoredPosition;
            position.x = Mathf.Lerp(_hiddenX, _shownX, _openAmount);
            _rectTransform.anchoredPosition = position;

            if (toggleButtonRect != null)
            {
                Vector2 buttonPosition = toggleButtonRect.anchoredPosition;
                buttonPosition.x = Mathf.Lerp(toggleButtonHiddenX, toggleButtonShownX, _openAmount);
                toggleButtonRect.anchoredPosition = buttonPosition;
            }
        }
    }
}
