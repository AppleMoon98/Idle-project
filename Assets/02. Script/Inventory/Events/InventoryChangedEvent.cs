using Equipment;

namespace Inventory.Events
{
    /// <summary>
    /// 인벤토리에 장비가 추가되어 목록이 변경되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct InventoryChangedEvent
    {
        /// <summary>
        /// 새로 추가된 장비.
        /// </summary>
        public EquipmentSO AddedEquipment { get; }

        /// <summary>
        /// 변경 후 인벤토리에 보관 중인 총 장비 개수.
        /// </summary>
        public int TotalCount { get; }

        public InventoryChangedEvent(EquipmentSO addedEquipment, int totalCount)
        {
            AddedEquipment = addedEquipment;
            TotalCount = totalCount;
        }
    }
}
