using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// 버튼을 누르고 있는 동안(포인터가 눌린 채 버튼 위에 있을 때)만 자식 라벨 Text의
    /// Bottom/Top 오프셋을 눌림 전용 값으로 바꾸고, 떼거나 버튼 밖으로 나가면 원래 값으로
    /// 되돌린다. Button.Transition(SpriteSwap)이 배경 스프라이트를 바꾸는 것과는 완전히
    /// 별개로, 같은 포인터 이벤트를 이 컴포넌트가 독립적으로 받아 라벨 위치만 조정한다 -
    /// 정상 상태 오프셋은 하드코딩하지 않고 Awake() 시점의 RectTransform 값을 그대로
    /// 캐싱해 사용한다(Text 오브젝트 자신의 offsetMin/Max가 유일한 소스).
    /// </summary>
    public sealed class ButtonPressedTextOffsetUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private RectTransform text;

        [SerializeField]
        private float pressedBottom;

        [SerializeField]
        private float pressedTop;

        private Vector2 _normalOffsetMin;
        private Vector2 _normalOffsetMax;
        private bool _isPointerDown;
        private bool _isPointerOver;

        private void Awake()
        {
            _normalOffsetMin = text.offsetMin;
            _normalOffsetMax = text.offsetMax;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPointerDown = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPointerDown = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            Refresh();
        }

        private void Refresh()
        {
            bool isPressed = _isPointerDown && _isPointerOver;
            text.offsetMin = new Vector2(_normalOffsetMin.x, isPressed ? pressedBottom : _normalOffsetMin.y);
            text.offsetMax = new Vector2(_normalOffsetMax.x, isPressed ? -pressedTop : _normalOffsetMax.y);
        }
    }
}
