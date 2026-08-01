namespace SoldierEquipment.Events
{
    /// <summary>
    /// 특정 병사 유닛의 특정 슬롯 장착 상태가 바뀌었을 때 EventBus를 통해 발행되는 이벤트.
    /// Inventory.Events.EquipmentEquippedEvent와 같은 이유로 세부 정보는 담지 않고,
    /// 구독자가 SoldierEquippedGearService에 다시 물어보는 방식이다.
    /// </summary>
    public readonly struct SoldierEquipmentEquippedEvent
    {
        /// <summary>
        /// 장착 상태가 바뀐 병사 유닛의 InstanceId.
        /// </summary>
        public int InstanceId { get; }

        /// <summary>
        /// 장착 상태가 바뀐 슬롯.
        /// </summary>
        public SoldierEquipmentType Slot { get; }

        public SoldierEquipmentEquippedEvent(int instanceId, SoldierEquipmentType slot)
        {
            InstanceId = instanceId;
            Slot = slot;
        }
    }
}
