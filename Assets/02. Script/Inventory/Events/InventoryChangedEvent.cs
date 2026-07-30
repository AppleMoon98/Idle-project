namespace Inventory.Events
{
    /// <summary>
    /// 인벤토리의 어떤 장비 라인(OwnedEquipment)이 변경되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct InventoryChangedEvent
    {
        /// <summary>
        /// 변경된 장비 라인. 보유개수/강화레벨의 현재 값을 그대로 담고 있다.
        /// </summary>
        public OwnedEquipment Changed { get; }

        /// <summary>
        /// 변경 후 인벤토리에 보관 중인 서로 다른 장비 라인의 총 개수.
        /// </summary>
        public int TotalDistinctItems { get; }

        public InventoryChangedEvent(OwnedEquipment changed, int totalDistinctItems)
        {
            Changed = changed;
            TotalDistinctItems = totalDistinctItems;
        }
    }
}
