using Character;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Attacker가 타겟을 찾았을 때 실제로 무엇을 할지(즉시 데미지, 투사체 발사 등)를 결정하는 전략.
    /// </summary>
    public interface IAttackBehavior
    {
        /// <summary>
        /// 공격을 실행한다.
        /// </summary>
        /// <param name="origin">공격을 시작하는 위치(공격자의 Transform)</param>
        /// <param name="target">공격 대상</param>
        /// <param name="attackPower">적용할 공격력</param>
        /// <param name="isCritical">치명타 여부</param>
        void Execute(Transform origin, Health target, float attackPower, bool isCritical);
    }
}
