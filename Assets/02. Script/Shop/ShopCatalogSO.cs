using System.Collections.Generic;
using UnityEngine;

namespace Shop
{
    /// <summary>
    /// 상점 물품 전체를 담는 평평한 카탈로그(다른 도메인의 카탈로그 SO와 같은 형태).
    /// 카테고리별 필터링은 UI가 매번 계산하지 않고 이 카탈로그에서 한 번에 처리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "Idle Project/Shop/Shop Catalog")]
    public sealed class ShopCatalogSO : ScriptableObject
    {
        [SerializeField]
        private ShopItemSO[] items;

        public ShopItemSO[] Items => items;

        public IEnumerable<ShopItemSO> GetByCategory(ShopCategory category)
        {
            for (int i = 0; i < items.Length; i++)
            {
                ShopItemSO item = items[i];

                if (item != null && item.Category == category)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// stableId가 일치하는 물품 원형을 반환한다. 없거나 stableId가 비어있으면 null.
        /// 세이브 데이터가 배열 인덱스 대신 이 값으로 항목을 식별할 때 쓴다(GitHub 이슈 #19).
        /// </summary>
        public ShopItemSO FindByStableId(string stableId)
        {
            if (items == null || string.IsNullOrEmpty(stableId))
            {
                return null;
            }

            foreach (ShopItemSO item in items)
            {
                if (item != null && item.StableId == stableId)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
