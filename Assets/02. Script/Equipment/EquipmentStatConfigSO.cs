using System;
using Enhancement;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 슬롯이 어떤 능력치를(<see cref="EnhancementStatType"/>) 얼마나 주는지 정의하는 데이터 에셋.
    /// 슬롯→능력치 매핑을 코드에 고정하지 않고 배열로 데이터화해, 기획 변경(예: 장갑도 공격력에
    /// 기여) 시 코드 수정 없이 이 에셋만 바꾸면 되도록 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentStatConfig", menuName = "Idle Project/Equipment/Equipment Stat Config")]
    public sealed class EquipmentStatConfigSO : ScriptableObject
    {
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
        }

        [SerializeField]
        private SlotStatEntry[] entries;

        /// <summary>
        /// slot에 대응하는 능력치 계수를 찾는다. 매핑이 없는 슬롯이면 false(그 슬롯은 스탯을 주지 않음).
        /// </summary>
        public bool TryGetEntry(EquipmentType slot, out SlotStatEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Slot == slot)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }
    }
}
