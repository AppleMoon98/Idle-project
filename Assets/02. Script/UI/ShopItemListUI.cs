using Shop;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 상점 카테고리 패널 하나가 갖는 물품 목록. catalog를 category로 필터링해 Awake에서
    /// 한 번만 행을 채운다 - 카탈로그가 런타임에 바뀌지 않는 정적 콘텐츠라 다시 그릴 필요가
    /// 없다(EquipmentSlotPopupUI 등 실시간 인벤토리 목록과 다른 지점).
    /// </summary>
    public sealed class ShopItemListUI : MonoBehaviour
    {
        [SerializeField]
        private ShopCatalogSO catalog;

        [SerializeField]
        private ShopCategory category;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private GameObject rowPrefab;

        private void Awake()
        {
            foreach (ShopItemSO item in catalog.GetByCategory(category))
            {
                GameObject rowGo = Instantiate(rowPrefab, rowContainer);
                rowGo.GetComponent<ShopItemRowUI>().Initialize(item);
            }
        }
    }
}
