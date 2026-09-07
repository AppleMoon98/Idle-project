using Shop;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 상점 카테고리 탭 버튼 하나의 라벨을 채운다. 이 버튼 자체는 씬에 고정 배치돼 있지만,
    /// StatRowUI 등 다른 *DisplayNames 소비자와 같은 관례로 문자열을 씬에 직접 박아두지 않고
    /// ShopCategoryDisplayNames에서 매번 가져온다 - 표시 이름이 바뀌어도 이 버튼을 다시 저장할
    /// 필요 없이 ShopCategoryDisplayNames.cs 한 곳만 고치면 된다.
    /// </summary>
    public sealed class ShopCategoryTabButtonUI : MonoBehaviour
    {
        [SerializeField]
        private ShopCategory category;

        [SerializeField]
        private Text label;

        private void Awake()
        {
            label.text = ShopCategoryDisplayNames.Get(category);
        }
    }
}
