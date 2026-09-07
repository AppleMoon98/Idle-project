using Shop;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 상점 물품 목록의 카드 한 장. 아이콘/이름만 채우고, 구매 버튼은 아직 로직이 없어
    /// DungeonPopup의 RelicDungeonRow와 같은 관례로 프리팹에서부터 비활성 "준비 중" 상태로
    /// 고정돼 있다(이 스크립트가 건드리지 않음).
    /// </summary>
    public sealed class ShopItemRowUI : MonoBehaviour
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
