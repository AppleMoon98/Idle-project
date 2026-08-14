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

        /// <summary>
        /// 쿨다운이 다 찼을 때 실제로 발동할 만한 대상이 있는지(공격형 스킬은 사거리 안 살아있는
        /// 적 존재 여부, 자기 버프처럼 대상이 필요 없는 스킬은 항상 true). SkillSlot이 쿨다운 완료
        /// 시점에 이 값으로 발동 여부를 결정한다 - 대상이 없으면 Execute를 아예 호출하지 않고
        /// 쿨다운을 소모하지도 않는다(다음 틱에 다시 확인, 대상이 나타나는 즉시 발동).
        /// </summary>
        bool HasTargetInRange(Transform origin, SkillSO definition);
    }
}
