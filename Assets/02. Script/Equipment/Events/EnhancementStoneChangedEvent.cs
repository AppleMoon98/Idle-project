namespace Equipment.Events
{
    /// <summary>
    /// 보유 강화석 총량이 변경되었을 때 EventBus를 통해 발행되는 이벤트. UI/Save 등이 구독한다.
    /// </summary>
    public readonly struct EnhancementStoneChangedEvent
    {
        /// <summary>
        /// 변경 후 보유 강화석 총량.
        /// </summary>
        public int CurrentStones { get; }

        public EnhancementStoneChangedEvent(int currentStones)
        {
            CurrentStones = currentStones;
        }
    }
}
