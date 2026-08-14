using System.Collections.Generic;
using Enhancement;

namespace Skill.Effects
{
    /// <summary>
    /// 저주 - 최근접 적 1체의 최대체력/공격력만 낮춘다(쇠약과 분리된 별도 디버프).
    /// 실제 타게팅/지속시간/원복 로직은 SingleTargetDebuffSkillEffect 참고.
    /// </summary>
    public sealed class CurseSkillEffect : SingleTargetDebuffSkillEffect
    {
        private static readonly EnhancementStatType[] Stats =
        {
            EnhancementStatType.MaxHealth,
            EnhancementStatType.AttackPower
        };

        protected override IReadOnlyList<EnhancementStatType> AffectedStats => Stats;
    }
}
