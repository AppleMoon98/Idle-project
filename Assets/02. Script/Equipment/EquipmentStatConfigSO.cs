using System;
using System.Collections.Generic;
using Enhancement;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 슬롯이 어떤 능력치를(<see cref="EnhancementStatType"/>) 얼마나 주는지 정의하는 데이터 에셋.
    /// 슬롯→능력치 매핑을 코드에 고정하지 않고 배열로 데이터화해, 기획 변경(예: 장갑도 공격력에
    /// 기여, 슬롯 하나가 능력치 여러 개를 동시에 주는 것) 시 코드 수정 없이 이 에셋만 바꾸면 되도록 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentStatConfig", menuName = "Idle Project/Equipment/Equipment Stat Config")]
    public sealed class EquipmentStatConfigSO : ScriptableObject
    {
        /// <summary>
        /// 등급 인덱스가 GradeIndexThreshold 이상일 때 적용되는 고정 보너스 한 구간.
        /// Enhancement.CostIncrementTier/EquipmentPossessionConfigSO.PossessionEnhancementTier와
        /// 같은 "구간별" 관례 - 여러 구간이 동시에 조건을 만족하면 가장 큰 GradeIndexThreshold를
        /// 가진 구간 하나만 적용된다(누적 합산 아님).
        /// </summary>
        [Serializable]
        public struct GradeThresholdBonusTier
        {
            public int GradeIndexThreshold;
            public float BonusAmount;
        }

        /// <summary>
        /// 슬롯 하나가 기여하는 능력치 하나의 계산 계수. 등급 인덱스(EquipmentGradeCatalogSO 기준)에
        /// 대한 선형 공식(BaseValue + PerGradeIndex * gradeIndex)으로 기본값을 정하고,
        /// 강화 배율은 EquipmentEnhancementConfigSO.StatBonusPerLevel을 별도로 곱해 적용한다.
        /// </summary>
        [Serializable]
        public struct SlotStatEntry
        {
            public EquipmentType Slot;
            public EnhancementStatType StatType;
            public float BaseValue;
            public float PerGradeIndex;

            /// <summary>
            /// 빈 배열(기본값)이면 완전히 무시된다 - 이 필드를 건드리지 않는 기존 엔트리는 전혀
            /// 영향받지 않는다. 강화 배율은 이 보너스가 baseline에 더해진 이후에 곱해지므로,
            /// 이 보너스도 함께 강화 스케일링을 받는다.
            /// </summary>
            public GradeThresholdBonusTier[] GradeThresholdBonuses;
        }

        [SerializeField]
        private SlotStatEntry[] entries;

        /// <summary>
        /// slot에 대응하는 능력치 계수를 모두 찾는다(같은 슬롯에 항목을 여러 개 두면 그 슬롯은
        /// 능력치를 여러 개 준다). 매핑이 없으면 빈 목록.
        /// </summary>
        public IEnumerable<SlotStatEntry> GetEntries(EquipmentType slot)
        {
            if (entries == null)
            {
                yield break;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Slot == slot)
                {
                    yield return entries[i];
                }
            }
        }
    }
}
