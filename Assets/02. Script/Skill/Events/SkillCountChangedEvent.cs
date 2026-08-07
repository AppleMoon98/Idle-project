namespace Skill.Events
{
    /// <summary>
    /// 스킬 보유 개수가 바뀌었을 때(획득 또는 레벨업 재료 소모) 발행되는 이벤트. UI가 이걸로
    /// 보유 개수 표시를 갱신한다.
    /// </summary>
    public readonly struct SkillCountChangedEvent
    {
        public SkillSO Definition { get; }

        public int NewCount { get; }

        public SkillCountChangedEvent(SkillSO definition, int newCount)
        {
            Definition = definition;
            NewCount = newCount;
        }
    }
}
