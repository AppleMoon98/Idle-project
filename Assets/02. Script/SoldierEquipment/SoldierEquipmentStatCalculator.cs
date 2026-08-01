using System;
using Enhancement;

namespace SoldierEquipment
{
    /// <summary>
    /// 병사 유닛 하나가 장착 중인 장비로부터 얻는 스탯 총합을 계산하는 순수 함수. Player의
    /// EquipmentStatService와 달리 상시 캐시/이벤트 방송을 하지 않는다 — 로스터 대부분은
    /// 배치되지 않은 상태이므로, 필요한 시점(배치 시, UI 표시 시)에 직접 호출해 계산한다.
    /// </summary>
    public static class SoldierEquipmentStatCalculator
    {
        /// <summary>
        /// instanceId 유닛이 장착 중인 모든 슬롯을 훑어 statType과 일치하는 보너스를 합산한다.
        /// </summary>
        public static float CalculateTotal(SoldierEquippedGearService equippedGear, int instanceId, EnhancementStatType statType)
        {
            float total = 0f;

            foreach (SoldierEquipmentType slot in (SoldierEquipmentType[])Enum.GetValues(typeof(SoldierEquipmentType)))
            {
                OwnedSoldierEquipment equipped = equippedGear.GetEquipped(instanceId, slot);

                if (equipped == null || equipped.Definition.StatBonuses == null)
                {
                    continue;
                }

                foreach (SoldierStatBonusEntry entry in equipped.Definition.StatBonuses)
                {
                    if (entry.StatType == statType)
                    {
                        total += entry.Value;
                    }
                }
            }

            return total;
        }
    }
}
