using Enhancement;

namespace Equipment.Events
{
    /// <summary>
    /// 장착 장비 전체로부터 오는 능력치 총합이 바뀌었을 때 EventBus를 통해 발행되는 이벤트.
    /// Character 쪽은 StatType/NewTotalBonus 숫자만 받아 RuntimeStats에 반영하면 되고,
    /// Equipment/Inventory 도메인 타입은 알 필요가 없다.
    /// </summary>
    public readonly struct EquipmentStatsChangedEvent
    {
        /// <summary>
        /// 변경된 능력치 종류.
        /// </summary>
        public EnhancementStatType StatType { get; }

        /// <summary>
        /// 장착 장비 전체로부터 오는 이 능력치의 새로운 총 보너스(누적치, 델타가 아니다).
        /// 구독자는 자신이 직전에 적용해둔 값과의 차이만큼만 RuntimeStats에 반영해야 한다.
        /// </summary>
        public float NewTotalBonus { get; }

        public EquipmentStatsChangedEvent(EnhancementStatType statType, float newTotalBonus)
        {
            StatType = statType;
            NewTotalBonus = newTotalBonus;
        }
    }
}
