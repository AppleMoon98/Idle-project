using UnityEngine;

namespace SoldierEquipment
{
    /// <summary>
    /// 병사 전용 장비 아이템의 데이터 에셋. 플레이어 Equipment.EquipmentSO와 완전히 분리된 도메인이며,
    /// 등급 없이 아이템 자신의 StatBonuses로 성능을 직접 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoldierEquipment", menuName = "Idle Project/Soldier Equipment/Soldier Equipment")]
    public sealed class SoldierEquipmentSO : ScriptableObject
    {
        [SerializeField]
        private string itemName;

        [SerializeField]
        private SoldierEquipmentType slotType;

        [SerializeField]
        private SoldierStatBonusEntry[] statBonuses;

        /// <summary>
        /// 아이템 이름.
        /// </summary>
        public string ItemName => itemName;

        /// <summary>
        /// 착용 슬롯 종류.
        /// </summary>
        public SoldierEquipmentType SlotType => slotType;

        /// <summary>
        /// 이 아이템이 제공하는 스탯 보너스 목록.
        /// </summary>
        public SoldierStatBonusEntry[] StatBonuses => statBonuses;
    }
}
