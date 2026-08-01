using System;
using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// BehaviorProfileSO 안의 규칙 한 줄: 조건과, 그 조건이 만족됐을 때 적용할 모드의 한 쌍.
    /// </summary>
    [Serializable]
    public sealed class BehaviorRuleEntry
    {
        [SerializeField]
        private BehaviorConditionSO condition;

        [SerializeField]
        private BehaviorMode mode;

        /// <summary>
        /// 이 규칙의 조건.
        /// </summary>
        public BehaviorConditionSO Condition => condition;

        /// <summary>
        /// 조건이 만족됐을 때 적용할 모드.
        /// </summary>
        public BehaviorMode Mode => mode;
    }
}
