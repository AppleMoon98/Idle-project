namespace Skill.Events
{
    /// <summary>
    /// SelfBuffSkillEffect가 시전자 자신에게 공격력 % 버프를 적용한 순간 함께 발행하는 이벤트.
    /// 병사(Soldier.SoldierStatReceiver)가 이걸 구독해 자기 자신의 현재 공격력 기준으로 같은
    /// 비율의 버프를 받는다 — SkillSlot/SelfBuffSkillEffect는 "병사"라는 개념을 전혀 모른 채로
    /// 그냥 이벤트만 던지고, 실제로 누가 반응할지는 구독자(병사) 쪽이 결정한다.
    /// </summary>
    public readonly struct SkillSelfBuffAppliedEvent
    {
        public float AttackPowerPercent { get; }

        public float Duration { get; }

        public SkillSelfBuffAppliedEvent(float attackPowerPercent, float duration)
        {
            AttackPowerPercent = attackPowerPercent;
            Duration = duration;
        }
    }
}
