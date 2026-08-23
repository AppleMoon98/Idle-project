using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// 버튼이 눌려있는 동안(PointerDown~PointerUp/Exit) 지정된 Label의 Top/Bottom 인셋을
    /// 바꿔(Top=pressedTopOffset, Bottom=0) 눌린 것처럼 아래로 붙여 보이게 한다.
    /// UI.BottomMenuUI.TabPressRelay(탭 바 전용, 패널이 열려있는 동안 계속 눌림 유지)와 같은
    /// 발상이지만, 탭 바에 속하지 않고 "누르는 동안만" 반응하면 되는 독립 버튼(예: SoldierPanel의
    /// DeploymentManageButton)에 붙여 쓴다 - 별도의 "계속 유지" 로직이 필요 없어 Unity 자체
    /// SpriteSwap 전이와 동일한 타이밍(PointerDown/Up/Exit)만 따라가면 충분하다.
    /// </summary>
    public sealed class ButtonPressedLabelUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField]
        private RectTransform label;

        [SerializeField]
        private float pressedTopOffset = 16f;

        private Vector2 _normalOffsetMin;
        private Vector2 _normalOffsetMax;

        private void Awake()
        {
            if (label != null)
            {
                _normalOffsetMin = label.offsetMin;
                _normalOffsetMax = label.offsetMax;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetPressed(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (label == null)
            {
                return;
            }

            if (pressed)
            {
                label.offsetMin = new Vector2(_normalOffsetMin.x, 0f);
                label.offsetMax = new Vector2(_normalOffsetMax.x, -pressedTopOffset);
            }
            else
            {
                label.offsetMin = _normalOffsetMin;
                label.offsetMax = _normalOffsetMax;
            }
        }
    }
}
