using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 강화 1회당 필요한 재료/비용과 증가량을 정의하는 데이터 에셋.
    /// 모든 장비 라인이 공유하는 하나의 공식이다(등급별로 다르게 하려면 나중에 배열로 바꾼다).
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentEnhancementConfig", menuName = "Idle Project/Equipment/Equipment Enhancement Config")]
    public sealed class EquipmentEnhancementConfigSO : ScriptableObject
    {
        [SerializeField]
        private int duplicatesRequiredPerLevel;

        [SerializeField]
        private int stoneCostBase;

        [SerializeField]
        private int stoneCostIncreasePerLevel;

        [SerializeField]
        private float statBonusPerLevel;

        [SerializeField]
        private int maxLevel;

        /// <summary>
        /// 강화 1회당 소모되는 중복 장비 개수(강화 대상 1개는 남기고 소모).
        /// </summary>
        public int DuplicatesRequiredPerLevel => duplicatesRequiredPerLevel;

        /// <summary>
        /// 1강화 시 필요한 강화석 비용.
        /// </summary>
        public int StoneCostBase => stoneCostBase;

        /// <summary>
        /// 강화 레벨당 강화석 비용 증가량.
        /// </summary>
        public int StoneCostIncreasePerLevel => stoneCostIncreasePerLevel;

        /// <summary>
        /// 강화 1회당 증가하는 능력치 비율. 착용 중일 때 EquipmentStatMath.CalculateBonus가 이 값을 실제로 적용한다.
        /// </summary>
        public float StatBonusPerLevel => statBonusPerLevel;

        /// <summary>
        /// 최대 강화 레벨.
        /// </summary>
        public int MaxLevel => maxLevel;
    }
}
