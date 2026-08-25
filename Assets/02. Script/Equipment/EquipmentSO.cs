using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 아이템의 데이터 에셋. 드롭/인벤토리 보관에 필요한 식별 정보를 가지며,
    /// 착용 상태는 Inventory.EquippedGearService가, 착용 시 스탯 적용은 Equipment.EquipmentStatService가 다룬다.
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment", menuName = "Idle Project/Equipment/Equipment")]
    public sealed class EquipmentSO : ScriptableObject
    {
        [SerializeField]
        private string stableId;

        [SerializeField]
        private string itemName;

        [SerializeField]
        private EquipmentType equipmentType;

        [SerializeField]
        private EquipmentGradeSO grade;

        [SerializeField]
        private Sprite icon;

        /// <summary>
        /// 카탈로그 배열 순서와 무관하게 이 항목을 영구적으로 식별하는 GUID(에디터 도구
        /// Editor.StableIdBackfill이 한 번 발급한 뒤로는 절대 바뀌지 않는다). 세이브 데이터가
        /// 이제 배열 인덱스 대신 이 값으로 "어떤 장비인지"를 기록한다(GitHub 이슈 #19 - 콘텐츠
        /// 재정렬/삭제 시 인덱스가 밀려 다른 항목을 가리키게 되는 문제를 근본적으로 막는다).
        /// </summary>
        public string StableId => stableId;

        /// <summary>
        /// 아이템 이름.
        /// </summary>
        public string ItemName => itemName;

        /// <summary>
        /// 착용 슬롯 종류.
        /// </summary>
        public EquipmentType EquipmentType => equipmentType;

        /// <summary>
        /// 이 장비의 등급단계. 합성(Fusion) 시 EquipmentGradeCatalogSO에서 다음 단계를 조회하는 기준이 된다.
        /// </summary>
        public EquipmentGradeSO Grade => grade;

        /// <summary>
        /// 목록 UI에 표시할 아이콘. 아직 지정되지 않았으면 null(현재는 등급 틴트로만 구분 표시됨).
        /// </summary>
        public Sprite Icon => icon;
    }
}
