using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 뽑기 결과 슬롯 하나 - 아이콘(있으면)과 등급색 테두리만 보여주는 순수 표시용 컴포넌트.
    /// border는 슬롯 전체를 채우는 배경 Image이고 icon은 그보다 작게 인셋된 자식이라, icon보다
    /// 남는 가장자리가 테두리처럼 보인다(EquippedSlotBarUI의 Frame과 동일한 방식).
    /// </summary>
    public sealed class GachaResultSlotUI : MonoBehaviour
    {
        [SerializeField]
        private Image border;

        [SerializeField]
        private Image icon;

        public void Initialize(GachaResultVisual visual)
        {
            border.color = visual.BorderColor;
            icon.sprite = visual.Icon;
            icon.enabled = visual.Icon != null;
        }
    }
}
