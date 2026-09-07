using UnityEngine;

namespace Shop
{
    /// <summary>
    /// 상점에서 파는 물품 하나의 정의. 실제 가격/보상 로직은 아직 없다 - 지금은
    /// 이름/아이콘/소속 카테고리만 갖는 순수 표시용 데이터다(메커니즘 우선, 콘텐츠는 나중).
    /// Icon은 sparse 필드다 - 비어있으면 UI가 자동으로 회색 placeholder로 표시한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItem", menuName = "Idle Project/Shop/Shop Item")]
    public sealed class ShopItemSO : ScriptableObject
    {
        [SerializeField]
        private string stableId;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private ShopCategory category;

        /// <summary>
        /// 카탈로그 배열 순서와 무관하게 이 항목을 영구적으로 식별하는 GUID(EquipmentSO 등과
        /// 동일한 관례, Editor.StableIdBackfill이 발급). 나중에 구매/보유를 저장하게 되면 배열
        /// 인덱스 대신 이 값으로 식별해야 콘텐츠 재정렬/삭제에도 세이브가 안전하다(GitHub 이슈 #19).
        /// </summary>
        public string StableId => stableId;

        public string DisplayName => displayName;

        public Sprite Icon => icon;

        public ShopCategory Category => category;
    }
}
