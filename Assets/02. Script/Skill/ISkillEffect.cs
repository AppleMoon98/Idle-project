using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 스킬 하나가 실제로 하는 일. SkillSlot이 쿨다운마다 이 인터페이스의 Execute만 호출하며,
    /// 어떤 효과인지(범위 데미지/버프/단일 강타 등)는 구현체가 전적으로 결정한다
    /// (Combat.IAttackBehavior와 동일한 전략 패턴 — 컴포지션으로 스킬 종류를 바꾼다).
    /// </summary>
    public interface ISkillEffect
    {
        /// <summary>
        /// origin: 스킬 시전자의 Transform(현재는 Player). definition: 장착된 스킬 데이터(사거리/지속시간/
        /// 이펙트 프리팹 등 스킬 고유 수치는 여기서 읽는다). magnitude: SkillSO.GetMagnitude(현재 레벨) 결과값.
        /// </summary>
        void Execute(Transform origin, SkillSO definition, float magnitude);
    }
}
