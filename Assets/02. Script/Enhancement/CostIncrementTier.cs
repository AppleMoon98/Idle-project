using System;
using UnityEngine;

namespace Enhancement
{
    /// <summary>
    /// 계단식 강화 비용 구간 하나. LevelThreshold 이상인 레벨부터 강화 1회당 Increment만큼
    /// 비용이 증가한다. EnhancementConfigSO.CostIncrementTiers가 비어있으면 이 구간 방식 대신
    /// 기존 복리(BaseCost * CostMultiplier^level) 방식을 사용한다.
    /// </summary>
    [Serializable]
    public sealed class CostIncrementTier
    {
        [SerializeField]
        private int levelThreshold;

        [SerializeField]
        private int increment;

        /// <summary>
        /// 이 구간이 시작되는 레벨.
        /// </summary>
        public int LevelThreshold => levelThreshold;

        /// <summary>
        /// 이 구간에서 강화 1회당 늘어나는 비용.
        /// </summary>
        public int Increment => increment;
    }
}
