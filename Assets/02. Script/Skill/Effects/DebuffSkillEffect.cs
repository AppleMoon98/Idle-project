using System.Collections.Generic;
using Enhancement;

namespace Skill.Effects
{
    /// <summary>
    /// 쇠약 - 최근접 적 1체의 이동속도/공격속도만 낮춘다. 최대체력/공격력 감소는
    /// CurseSkillEffect(저주)로 분리됐다. 실제 타게팅/지속시간/원복 로직은
    /// SingleTargetDebuffSkillEffect 참고.
    /// </summary>
    public sealed class DebuffSkillEffect : SingleTargetDebuffSkillEffect
    {
        private static readonly EnhancementStatType[] Stats =
        {
            EnhancementStatType.MoveSpeed,
            EnhancementStatType.AttackSpeed
        };

        protected override IReadOnlyList<EnhancementStatType> AffectedStats => Stats;
    }
}
