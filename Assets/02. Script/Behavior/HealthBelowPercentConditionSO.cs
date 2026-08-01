using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// 현재 체력 비율이 threshold 미만이면 만족되는 조건. 후퇴 규칙의 트리거로 쓰인다.
    /// </summary>
    [CreateAssetMenu(fileName = "HealthBelowPercentCondition", menuName = "Idle Project/Behavior/Conditions/Health Below Percent")]
    public sealed class HealthBelowPercentConditionSO : BehaviorConditionSO
    {
        [SerializeField]
        [Range(0f, 1f)]
        private float threshold = 0.3f;

        public override bool Evaluate(BehaviorContext context)
        {
            return context.HealthPercent < threshold;
        }
    }
}
