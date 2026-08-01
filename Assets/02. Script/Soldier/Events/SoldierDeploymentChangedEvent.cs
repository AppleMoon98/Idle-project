namespace Soldier.Events
{
    /// <summary>
    /// 특정 배치 슬롯의 배정 상태(어떤 유닛이 나가 있는지)가 변경되었을 때 발행되는 이벤트.
    /// </summary>
    public readonly struct SoldierDeploymentChangedEvent
    {
        /// <summary>
        /// 배정 상태가 바뀐 슬롯.
        /// </summary>
        public int SlotIndex { get; }

        public SoldierDeploymentChangedEvent(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
}
