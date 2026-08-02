using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 스킬 하나(고정 슬롯)의 데이터 정의. 레벨 0(미강화 상태)에도 EffectValueBase만큼은 항상
    /// 발동하며, 레벨이 오를수록 EffectValuePerLevel만큼 수치가 커진다. 비용은 골드/강화석을
    /// 둘 다 요구하며 레벨에 비례해 선형으로 증가한다(EquipmentEnhancementConfigSO와 동일한 형태).
    /// </summary>
    [CreateAssetMenu(fileName = "Skill", menuName = "Idle Project/Skill/Skill")]
    public sealed class SkillSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private float cooldown = 8f;

        [SerializeField]
        private int maxLevel = 20;

        [SerializeField]
        private int goldCostBase = 100;

        [SerializeField]
        private int goldCostIncreasePerLevel = 50;

        [SerializeField]
        private int stoneCostBase = 5;

        [SerializeField]
        private int stoneCostIncreasePerLevel = 2;

        [SerializeField]
        private float effectValueBase = 10f;

        [SerializeField]
        private float effectValuePerLevel = 2f;

        /// <summary>
        /// 스킬 이름(UI 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 자동 발동 주기(초).
        /// </summary>
        public float Cooldown => cooldown;

        /// <summary>
        /// 최대 레벨.
        /// </summary>
        public int MaxLevel => maxLevel;

        /// <summary>
        /// 레벨(0부터 시작)에 따른 효과 수치. ISkillEffect 구현체가 데미지/버프량 등으로 해석한다.
        /// </summary>
        public float GetMagnitude(int level)
        {
            return effectValueBase + effectValuePerLevel * level;
        }

        /// <summary>
        /// 다음 레벨(level -> level+1)로 올리는 데 필요한 골드 비용.
        /// </summary>
        public int GetGoldCost(int level)
        {
            return goldCostBase + goldCostIncreasePerLevel * level;
        }

        /// <summary>
        /// 다음 레벨(level -> level+1)로 올리는 데 필요한 강화석 비용.
        /// </summary>
        public int GetStoneCost(int level)
        {
            return stoneCostBase + stoneCostIncreasePerLevel * level;
        }
    }
}
