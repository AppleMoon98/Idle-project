using Equipment;

namespace Inventory.Events
{
    /// <summary>
    /// 특정 슬롯의 장착 장비가 바뀌었을 때 EventBus를 통해 발행되는 이벤트.
    /// 세부 정보(어떤 장비인지 등)는 이벤트에 담지 않고, 구독자가 EquippedGearService에
    /// 다시 물어보는 방식 — InventoryChangedEvent와 달리 "무엇이" 바뀌었는지만 알려준다.
    /// </summary>
    public readonly struct EquipmentEquippedEvent
    {
        /// <summary>
        /// 장착 상태가 바뀐 슬롯.
        /// </summary>
        public EquipmentType Slot { get; }

        public EquipmentEquippedEvent(EquipmentType slot)
        {
            Slot = slot;
        }
    }
}
