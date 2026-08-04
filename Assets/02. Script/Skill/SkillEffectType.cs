namespace Skill
{
    /// <summary>
    /// 스킬이 실제로 어떤 효과를 내는지 구분하는 종류. SkillSlot이 장착된 SkillSO의 이 값으로
    /// 자신이 들고 있는 여러 ISkillEffect 구현체 중 어느 것을 실행할지 고른다.
    /// </summary>
    public enum SkillEffectType
    {
        AreaDamage,
        SingleTargetStrike,
        SelfBuff
    }
}
