namespace Loot.Events
{
    /// <summary>
    /// 몬스터 처치로 골드 획득이 확정되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct GoldEarnedEvent
    {
        /// <summary>
        /// 획득한 골드 수량.
        /// </summary>
        public int Amount { get; }

        public GoldEarnedEvent(int amount)
        {
            Amount = amount;
        }
    }
}
