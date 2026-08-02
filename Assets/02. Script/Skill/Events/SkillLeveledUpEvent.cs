namespace Skill.Events
{
    /// <summary>
    /// 스킬 레벨이 올랐을 때 발행되는 이벤트. UI가 이걸로 표시를 갱신한다.
    /// </summary>
    public readonly struct SkillLeveledUpEvent
    {
        public SkillSO Definition { get; }

        public int NewLevel { get; }

        public SkillLeveledUpEvent(SkillSO definition, int newLevel)
        {
            Definition = definition;
            NewLevel = newLevel;
        }
    }
}
