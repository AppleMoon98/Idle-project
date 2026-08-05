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
        private string itemName;

        [SerializeField]
        private EquipmentType equipmentType;

        [SerializeField]
        private EquipmentGradeSO grade;

        [SerializeField]
        private Sprite icon;

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
