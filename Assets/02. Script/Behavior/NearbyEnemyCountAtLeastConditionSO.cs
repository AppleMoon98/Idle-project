using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// scanRadius 안의 적 수가 requiredCount 이상이면 만족되는 조건. "적이 몰려오면 다르게 행동" 같은
    /// 규칙에 쓰인다. 반경은 조건마다 다르게 설정할 수 있도록 이 SO 자체에 직렬화한다
    /// (BehaviorContext는 위치/레이어만 제공하고, 얼마나 넓게 볼지는 조건의 몫).
    /// </summary>
    [CreateAssetMenu(fileName = "NearbyEnemyCountAtLeastCondition", menuName = "Idle Project/Behavior/Conditions/Nearby Enemy Count At Least")]
    public sealed class NearbyEnemyCountAtLeastConditionSO : BehaviorConditionSO
    {
        [SerializeField]
        private int requiredCount = 3;

        [SerializeField]
        private float scanRadius = 5f;

        public override bool Evaluate(BehaviorContext context)
        {
            return Physics2D.OverlapCircleAll(context.Position, scanRadius, context.EnemyLayerMask).Length >= requiredCount;
        }
    }
}
