using UnityEngine;

namespace Enhancement
{
    /// <summary>
    /// 능력치 하나를 강화하는 데 필요한 비용/증가량 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "EnhancementConfig", menuName = "Idle Project/Enhancement/Enhancement Config")]
    public sealed class EnhancementConfigSO : ScriptableObject
    {
        [SerializeField]
        private EnhancementStatType statType;

        [SerializeField]
        private int baseCost;

        [SerializeField]
        private float costMultiplier = 1.5f;

        [SerializeField]
        private float valuePerLevel;

        [SerializeField]
        private int maxLevel;

        /// <summary>
        /// 강화 대상 능력치.
        /// </summary>
        public EnhancementStatType StatType => statType;

        /// <summary>
        /// 1레벨 강화 비용.
        /// </summary>
        public int BaseCost => baseCost;

        /// <summary>
        /// 레벨당 비용 배율(복리). 다음 비용 = BaseCost * CostMultiplier^현재레벨.
        /// </summary>
        public float CostMultiplier => costMultiplier;

        /// <summary>
        /// 강화 1회당 능력치 증가량.
        /// </summary>
        public float ValuePerLevel => valuePerLevel;

        /// <summary>
        /// 최대 강화 레벨.
        /// </summary>
        public int MaxLevel => maxLevel;
    }
}
