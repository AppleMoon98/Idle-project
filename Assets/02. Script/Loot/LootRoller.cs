using System.Collections.Generic;
using Equipment;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// 골드/장비 드롭을 확률 판정하는 순수 굴림 로직.
    /// 실시간 사망 처리(LootDropper)와 오프라인 보상 시뮬레이션이 동일한 확률로
    /// 굴릴 수 있도록 공통으로 사용한다.
    /// </summary>
    public static class LootRoller
    {
        /// <summary>
        /// 골드 드롭을 판정한다. 실패하면 null. multiplier(기본 1)는 MonsterLootSO의 기본
        /// Min/Max 범위로 굴린 값에 그대로 곱해진다 - Stage.StageDifficultyConfigSO.GetGoldMultiplier로
        /// 스테이지가 진행될수록 점진적으로 더 많은 골드를 주도록 스케일링할 때 쓴다.
        /// </summary>
        public static int? RollGold(MonsterLootSO loot, float multiplier = 1f)
        {
            if (Random.value > loot.DropChance)
            {
                return null;
            }

            int baseAmount = Random.Range(loot.MinGold, loot.MaxGold + 1);
            return Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));
        }

        /// <summary>
        /// 장비 드롭 테이블을 각 항목 독립적으로 판정해, 성공한 장비들을 반환한다.
        /// entries가 비어있으면(예: 장비 드롭이 아직 시작되지 않은 스테이지) 아무것도 반환하지 않는다.
        /// </summary>
        public static IEnumerable<EquipmentSO> RollEquipment(EquipmentDropEntry[] entries)
        {
            if (entries == null)
            {
                yield break;
            }

            foreach (EquipmentDropEntry entry in entries)
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
