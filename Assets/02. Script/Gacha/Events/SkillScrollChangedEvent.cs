namespace Gacha.Events
{
    /// <summary>
    /// 보유 스킬 주문서 수량이 변경되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct SkillScrollChangedEvent
    {
        /// <summary>
        /// 변경 후 현재 보유 수량.
        /// </summary>
        public int CurrentScrolls { get; }

        public SkillScrollChangedEvent(int currentScrolls)
        {
            CurrentScrolls = currentScrolls;
        }
    }
}
