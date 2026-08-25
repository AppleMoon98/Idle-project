using Enhancement;

namespace Skill.Events
{
    /// <summary>
    /// SelfBuffSkillEffect/SoldierBuffSkillEffect/PartyHealBuffSkillEffect가 시전자 본인 또는
    /// 병사에게 스탯 % 버프를 적용한 순간 함께 발행하는 이벤트. 병사(Soldier.SoldierStatReceiver)가
    /// 이걸 구독해 자기 자신의 현재 StatType 값 기준으로 같은 비율의 버프를 받는다 — Skill 도메인은
    /// "병사"라는 개념을 전혀 모른 채로 그냥 이벤트만 던지고, 실제로 누가 반응할지는 구독자
    /// (병사) 쪽이 결정한다. 원래 공격력 전용(AttackPowerPercent)이었으나, 전장의 외침(이속/공속)
    /// 처럼 다른 스탯을 버프하는 스킬이 늘어나면서 StatType을 받는 범용 형태로 일반화했다.
    /// </summary>
    public readonly struct SkillSelfBuffAppliedEvent
    {
        /// <summary>
        /// 버프가 적용될 스탯 종류.
        /// </summary>
        public EnhancementStatType StatType { get; }

        /// <summary>
        /// 현재 값 대비 버프 비율(예: 0.1 = +10%).
        /// </summary>
        public float Percent { get; }

        public float Duration { get; }

        /// <summary>
        /// 이 버프를 발행한 스킬. 구독자가 "같은 스킬의 재시전(갱신)"과 "다른 스킬이 같은 스탯을
        /// 동시에 버프(곱연산 중첩)"를 구분하는 키로 쓴다 — 같은 Source가 다시 들어오면 자기
        /// 자신의 이전 적용분만 되돌리고 새로 적용하고(무한 중첩 방지), 다른 Source면 서로의
        /// 델타를 건드리지 않아 현재 값(이미 다른 스킬 버프가 반영된 값) 기준으로 곱연산처럼 쌓인다.
        /// </summary>
        public SkillSO Source { get; }

        public SkillSelfBuffAppliedEvent(EnhancementStatType statType, float percent, float duration, SkillSO source)
        {
            StatType = statType;
            Percent = percent;
            Duration = duration;
            Source = source;
        }
    }
}
