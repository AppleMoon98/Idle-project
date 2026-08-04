namespace Skill.Events
{
    /// <summary>
    /// 장착 슬롯의 자동 발동 켜짐/꺼짐 상태가 바뀌었을 때 EventBus를 통해 발행되는 이벤트.
    /// 슬롯에 어떤 스킬이 장착됐는지(SkillLoadoutChangedEvent)와는 별개 개념이다 - 꺼진 슬롯은
    /// 장착 상태를 유지한 채 그냥 자동 발동만 하지 않는다.
    /// </summary>
    public readonly struct SkillSlotEnabledChangedEvent
    {
        /// <summary>
        /// 바뀐 슬롯 인덱스(0~SkillLoadoutService.SlotCount-1).
        /// </summary>
        public int SlotIndex { get; }

        /// <summary>
        /// 변경 후 켜짐 여부.
        /// </summary>
        public bool IsEnabled { get; }

        public SkillSlotEnabledChangedEvent(int slotIndex, bool isEnabled)
        {
            SlotIndex = slotIndex;
            IsEnabled = isEnabled;
        }
    }
}
