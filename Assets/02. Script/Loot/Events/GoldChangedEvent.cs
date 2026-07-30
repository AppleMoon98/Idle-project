namespace Loot.Events
{
    /// <summary>
    /// 보유 골드 총량이 변경되었을 때 EventBus를 통해 발행되는 이벤트. UI 등이 구독한다.
    /// </summary>
    public readonly struct GoldChangedEvent
    {
        /// <summary>
        /// 변경 후 보유 골드 총량.
        /// </summary>
        public int CurrentGold { get; }

        public GoldChangedEvent(int currentGold)
        {
            CurrentGold = currentGold;
        }
    }
}
