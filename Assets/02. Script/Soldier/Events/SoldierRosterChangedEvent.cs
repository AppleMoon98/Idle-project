namespace Soldier.Events
{
    /// <summary>
    /// 병사 로스터에 새 유닛이 추가되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct SoldierRosterChangedEvent
    {
        /// <summary>
        /// 새로 추가된 유닛.
        /// </summary>
        public OwnedSoldier Added { get; }

        /// <summary>
        /// 변경 후 로스터에 보유 중인 유닛의 총 개수.
        /// </summary>
        public int TotalCount { get; }

        public SoldierRosterChangedEvent(OwnedSoldier added, int totalCount)
        {
            Added = added;
            TotalCount = totalCount;
        }
    }
}
