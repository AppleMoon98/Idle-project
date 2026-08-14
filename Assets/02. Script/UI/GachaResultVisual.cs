using UnityEngine;

namespace UI
{
    /// <summary>
    /// 뽑기 결과 슬롯 하나를 그리는 데 필요한 최소 정보. 장비/병사/스킬처럼 서로 다른 데이터
    /// 타입을 GachaResultSlotUI/GachaResultRevealController가 몰라도 되게 하는 공통 표현이다 -
    /// 각 *GachaTierPanelUI가 자기 데이터 타입에서 이 구조체로 변환해 넘긴다.
    /// </summary>
    public readonly struct GachaResultVisual
    {
        public Sprite Icon { get; }
        public Color BorderColor { get; }

        public GachaResultVisual(Sprite icon, Color borderColor)
        {
            Icon = icon;
            BorderColor = borderColor;
        }
    }
}
