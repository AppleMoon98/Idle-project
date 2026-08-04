namespace Skill.Events
{
    /// <summary>
    /// 스킬 장착 슬롯의 내용이 바뀌었을 때(장착/해제/자동 교체) EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct SkillLoadoutChangedEvent
    {
        /// <summary>
        /// 바뀐 슬롯 인덱스(0~SkillLoadoutService.SlotCount-1).
        /// </summary>
        public int SlotIndex { get; }

        /// <summary>
        /// 이제 이 슬롯에 장착된 스킬. 해제됐으면 null.
        /// </summary>
        public SkillSO Definition { get; }

        public SkillLoadoutChangedEvent(int slotIndex, SkillSO definition)
        {
            SlotIndex = slotIndex;
            Definition = definition;
        }
    }
}
