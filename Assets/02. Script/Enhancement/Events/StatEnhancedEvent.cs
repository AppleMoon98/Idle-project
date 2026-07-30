namespace Enhancement.Events
{
    /// <summary>
    /// 능력치 강화가 성공했을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct StatEnhancedEvent
    {
        /// <summary>
        /// 강화된 능력치 종류.
        /// </summary>
        public EnhancementStatType StatType { get; }

        /// <summary>
        /// 이번 강화로 적용할 증가량. 구독자가 자신의 RuntimeStats에 이 값을 더한다.
        /// </summary>
        public float ValuePerLevel { get; }

        /// <summary>
        /// 강화 후 레벨.
        /// </summary>
        public int NewLevel { get; }

        public StatEnhancedEvent(EnhancementStatType statType, float valuePerLevel, int newLevel)
        {
            StatType = statType;
            ValuePerLevel = valuePerLevel;
            NewLevel = newLevel;
        }
    }
}
