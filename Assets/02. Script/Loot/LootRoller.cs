using System.Collections.Generic;
using Equipment;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// MonsterLootSO 하나에 대해 골드/장비 드롭을 확률 판정하는 순수 굴림 로직.
    /// 실시간 사망 처리(LootDropper)와 오프라인 보상 시뮬레이션이 동일한 확률로
    /// 굴릴 수 있도록 공통으로 사용한다.
    /// </summary>
    public static class LootRoller
    {
        /// <summary>
        /// 골드 드롭을 판정한다. 실패하면 null.
        /// </summary>
        public static int? RollGold(MonsterLootSO loot)
        {
            if (Random.value > loot.DropChance)
            {
                return null;
            }

            return Random.Range(loot.MinGold, loot.MaxGold + 1);
        }

        /// <summary>
        /// 장비 드롭 테이블을 각 항목 독립적으로 판정해, 성공한 장비들을 반환한다.
        /// </summary>
        public static IEnumerable<EquipmentSO> RollEquipment(MonsterLootSO loot)
        {
            if (loot.EquipmentDrops == null)
            {
                yield break;
            }

            foreach (EquipmentDropEntry entry in loot.EquipmentDrops)
            {
                if (entry.Equipment == null || Random.value > entry.DropChance)
                {
                    continue;
                }

                yield return entry.Equipment;
            }
        }
    }
}
