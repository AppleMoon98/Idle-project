using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// BehaviorConditionSO.Evaluate가 판정에 필요로 하는 값들의 스냅샷. SoldierBehaviorController가
    /// 매 판정 주기마다 새로 구성해서 넘긴다.
    /// </summary>
    public readonly struct BehaviorContext
    {
        /// <summary>
        /// 현재 체력 비율(0~1).
        /// </summary>
        public float HealthPercent { get; }

        /// <summary>
        /// 판정 주체의 현재 위치.
        /// </summary>
        public Vector2 Position { get; }

        /// <summary>
        /// 적으로 간주할 레이어. 주변 적 수를 세는 조건 등이 사용한다.
        /// </summary>
        public LayerMask EnemyLayerMask { get; }

        public BehaviorContext(float healthPercent, Vector2 position, LayerMask enemyLayerMask)
        {
            HealthPercent = healthPercent;
            Position = position;
            EnemyLayerMask = enemyLayerMask;
        }
    }
}
