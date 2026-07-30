using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 아이템의 데이터 에셋. 착용/스탯 적용은 이후 Equipment 시스템에서 다루고,
    /// 이 단계에서는 드롭/인벤토리 보관에 필요한 식별 정보만 가진다.
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment", menuName = "Idle Project/Equipment/Equipment")]
    public sealed class EquipmentSO : ScriptableObject
    {
        [SerializeField]
        private string itemName;

        [SerializeField]
        private EquipmentType equipmentType;

        /// <summary>
        /// 아이템 이름.
        /// </summary>
        public string ItemName => itemName;

        /// <summary>
        /// 착용 슬롯 종류.
        /// </summary>
        public EquipmentType EquipmentType => equipmentType;
    }
}
