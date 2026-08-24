using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 버튼의 "평상시(Image.sprite)/눌림(Button.spriteState.pressedSprite)" 스프라이트 한 쌍을
    /// 통째로 다른 색 테마로 바꿔치기한다. 돌파/반복, 자동/수동처럼 버튼 자체가 주의를 끌어야
    /// 하는 상태(반복/수동)일 때 기본(파랑)에서 강조(빨강)로 바꾸는 용도 - 이 프로젝트의
    /// Button.Transition=SpriteSwap은 한 시점에 한 쌍의 스프라이트만 쓸 수 있어서, 상태가 바뀔
    /// 때마다 그 쌍 자체(Image.sprite + Button.spriteState.pressedSprite)를 갈아끼우는 방식으로
    /// 구현했다 - Button 자신의 SpriteSwap 로직(눌림 여부에 따라 이 컴포넌트가 지정해둔 두
    /// 스프라이트 사이를 오가는 것)은 전혀 건드리지 않는다.
    /// </summary>
    public sealed class ButtonColorThemeSwitcherUI : MonoBehaviour
    {
        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Button button;

        [SerializeField]
        private Sprite defaultRegularSprite;

        [SerializeField]
        private Sprite defaultPressedSprite;

        [SerializeField]
        private Sprite alertRegularSprite;

        [SerializeField]
        private Sprite alertPressedSprite;

        public void SetAlert(bool isAlert)
        {
            backgroundImage.sprite = isAlert ? alertRegularSprite : defaultRegularSprite;

            SpriteState state = button.spriteState;
            state.pressedSprite = isAlert ? alertPressedSprite : defaultPressedSprite;
            button.spriteState = state;
        }
    }
}
