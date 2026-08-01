using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// 병사 행동 규칙 하나의 조건을 판정하는 데이터 에셋의 기반 클래스. 구체적인 판정 로직은
    /// 하위 클래스(AlwaysConditionSO 등)가 구현하며, BehaviorProfileSO의 규칙 목록에서
    /// 종류에 상관없이 조합해 쓸 수 있다.
    /// </summary>
    public abstract class BehaviorConditionSO : ScriptableObject
    {
        /// <summary>
        /// context가 주어졌을 때 이 조건이 만족되는지 판정한다.
        /// </summary>
        public abstract bool Evaluate(BehaviorContext context);
    }
}
