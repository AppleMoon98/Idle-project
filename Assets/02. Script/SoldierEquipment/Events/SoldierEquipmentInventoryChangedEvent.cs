namespace SoldierEquipment.Events
{
    /// <summary>
    /// 병사 전용 장비 보유 재고의 어떤 라인(OwnedSoldierEquipment)이 변경되었을 때
    /// EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct SoldierEquipmentInventoryChangedEvent
    {
        /// <summary>
        /// 변경된 장비 라인. 보유개수의 현재 값을 그대로 담고 있다.
        /// </summary>
        public OwnedSoldierEquipment Changed { get; }

        /// <summary>
        /// 변경 후 보관 중인 서로 다른 장비 라인의 총 개수.
        /// </summary>
        public int TotalDistinctItems { get; }

        public SoldierEquipmentInventoryChangedEvent(OwnedSoldierEquipment changed, int totalDistinctItems)
        {
            Changed = changed;
            TotalDistinctItems = totalDistinctItems;
        }
    }
}
