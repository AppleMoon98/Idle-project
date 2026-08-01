using Soldier;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 가챠 확률 테이블에서 가중치 기반으로 병사 하나를 뽑는 순수 굴림 로직.
    /// LootRoller와 같은 이유로 정적 함수로 분리한다(실시간 뽑기와 향후 시뮬레이션이
    /// 동일한 확률로 굴릴 수 있도록).
    /// </summary>
    public static class GachaRoller
    {
        /// <summary>
        /// entries의 가중치 합 대비 비율로 병사 하나를 뽑는다. entries가 비어있거나
        /// 가중치 합이 0 이하이면(콘텐츠 미비) null.
        /// </summary>
        public static SoldierSO RollWeighted(GachaPoolEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return null;
            }

            int totalWeight = 0;

            foreach (GachaPoolEntry entry in entries)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (GachaPoolEntry entry in entries)
            {
                cumulative += entry.Weight;

                if (roll < cumulative)
                {
                    return entry.Soldier;
                }
            }

            return null;
        }
    }
}
