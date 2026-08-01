namespace Soldier.Events
{
    /// <summary>
    /// 특정 병사 유닛에 배정된 행동 프로필이 변경되었을 때 발행되는 이벤트.
    /// </summary>
    public readonly struct SoldierBehaviorProfileChangedEvent
    {
        /// <summary>
        /// 프로필이 바뀐 유닛의 InstanceId.
        /// </summary>
        public int InstanceId { get; }

        public SoldierBehaviorProfileChangedEvent(int instanceId)
        {
            InstanceId = instanceId;
        }
    }
}
