using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// 항상 만족되는 조건. 프로필의 마지막 규칙(기본 행동)으로 사용해, 앞의 조건이
    /// 하나도 만족되지 않았을 때의 폴백을 표현한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AlwaysCondition", menuName = "Idle Project/Behavior/Conditions/Always")]
    public sealed class AlwaysConditionSO : BehaviorConditionSO
    {
        public override bool Evaluate(BehaviorContext context)
        {
            return true;
        }
    }
}
