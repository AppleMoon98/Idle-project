using Shop;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 상점 물품 그리드의 슬롯 한 칸(SkillGridCell과 같은 "아이콘 중심 정사각 슬롯" 형태).
    /// 아이콘/이름만 채우고, 구매 로직은 아직 없어 우측 상단 "준비 중" 배지는 프리팹에서부터
    /// 고정돼 있다(이 스크립트가 건드리지 않음).
    /// </summary>
    public sealed class ShopItemSlotUI : MonoBehaviour
    {
        private static readonly Color PlaceholderIconColor = new(0.4245283f, 0.4245283f, 0.4245283f, 1f);

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Text nameText;

        public void Initialize(ShopItemSO item)
        {
            nameText.text = item.DisplayName;
            iconImage.sprite = item.Icon;
            iconImage.color = item.Icon != null ? Color.white : PlaceholderIconColor;
        }
    }
}
