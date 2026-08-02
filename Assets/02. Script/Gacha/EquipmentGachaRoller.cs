using Equipment;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 무기 가챠 확률 테이블에서 가중치 기반으로 장비 하나를 뽑는 순수 굴림 로직.
    /// GachaRoller(병사)와 동일한 알고리즘의 병렬 구현.
    /// </summary>
    public static class EquipmentGachaRoller
    {
        /// <summary>
        /// entries의 가중치 합 대비 비율로 장비 하나를 뽑는다. entries가 비어있거나
        /// 가중치 합이 0 이하이면(콘텐츠 미비) null.
        /// </summary>
        public static EquipmentSO RollWeighted(EquipmentGachaPoolEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return null;
            }

            int totalWeight = 0;

            foreach (EquipmentGachaPoolEntry entry in entries)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (EquipmentGachaPoolEntry entry in entries)
            {
                cumulative += entry.Weight;

                if (roll < cumulative)
                {
                    return entry.Equipment;
                }
            }

            return null;
        }
    }
}
