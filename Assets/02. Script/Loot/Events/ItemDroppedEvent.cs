using Equipment;

namespace Loot.Events
{
    /// <summary>
    /// 몬스터 처치로 장비 드롭이 확정되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct ItemDroppedEvent
    {
        /// <summary>
        /// 드롭된 장비.
        /// </summary>
        public EquipmentSO Equipment { get; }

        public ItemDroppedEvent(EquipmentSO equipment)
        {
            Equipment = equipment;
        }
    }
}
