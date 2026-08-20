namespace Dungeon.Events
{
    /// <summary>
    /// 보유 보스 토벌 증표 수량이 변경되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct BossTokenChangedEvent
    {
        /// <summary>
        /// 변경 후 현재 보유 수량.
        /// </summary>
        public int CurrentTokens { get; }

        public BossTokenChangedEvent(int currentTokens)
        {
            CurrentTokens = currentTokens;
        }
    }
}
