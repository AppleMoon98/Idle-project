namespace Skill.Events
{
    /// <summary>
    /// PartyHealBuffSkillEffect(전투찬가)가 시전 시 함께 발행하는 이벤트. 병사(Soldier.
    /// SoldierStatReceiver)가 이걸 구독해 자기 자신의 최대체력 대비 HealPercentPerSecond만큼을
    /// 매초 회복한다 - 플레이어 자신의 회복은 PartyHealBuffSkillEffect가 직접 처리하고, 이 이벤트는
    /// 병사에게 알리는 용도로만 쓰인다(SkillSelfBuffAppliedEvent와 같은 방향).
    /// </summary>
    public readonly struct SkillPartyHealAppliedEvent
    {
        /// <summary>
        /// 매초 회복할 양(자기 자신의 최대체력 대비 비율, 예: 0.02 = 초당 최대체력의 2%).
        /// </summary>
        public float HealPercentPerSecond { get; }

        public float Duration { get; }

        public SkillPartyHealAppliedEvent(float healPercentPerSecond, float duration)
        {
            HealPercentPerSecond = healPercentPerSecond;
            Duration = duration;
        }
    }
}
