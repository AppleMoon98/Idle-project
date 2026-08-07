using Enhancement;

namespace Equipment.Events
{
    /// <summary>
    /// 보유 중인(장착 여부 무관) 장비 라인 전체로부터 오는 능력치 총합이 바뀌었을 때 발행되는
    /// 이벤트. EquipmentStatsChangedEvent(장착 시 효과)와 같은 형태 — 값은 델타가 아니라
    /// 누적 총합이고, 구독자가 직전에 적용해둔 값과의 차이만큼만 반영해야 한다.
    /// </summary>
    public readonly struct EquipmentPossessionStatsChangedEvent
    {
        public EnhancementStatType StatType { get; }

        public float NewTotalPercent { get; }

        public EquipmentPossessionStatsChangedEvent(EnhancementStatType statType, float newTotalPercent)
        {
            StatType = statType;
            NewTotalPercent = newTotalPercent;
        }
    }
}
