using Loot;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 하나를 완전히 클리어했을 때 몬스터들에게서 얻을 수 있는 골드 총합의 최소~최대
    /// 범위를 계산한다(스테이지 자체의 골드 배율 적용 전, 순수 몬스터 구성 기준 - 배율은 호출하는
    /// 쪽이 곱한다). 일반 웨이브(SpawnEntries)는 각 엔트리의 프리팹이 가진
    /// Loot.MonsterLootProvider.Loot를 그대로 합산하고, 전술 웨이브(TacticEntries)는 리더는 항상
    /// LeaderPrefab 골드로, 추종자는 FollowerPrefab과 AlternateFollowerPrefab 중 대체 확률과
    /// 무관하게 이론상 나올 수 있는 더 낮은/높은 쪽을 각각 최소/최대에 반영한다.
    /// </summary>
    public static class StageGoldRangeCalculator
    {
        public static void Calculate(StageSO stage, out int totalMin, out int totalMax)
        {
            totalMin = 0;
            totalMax = 0;

            if (stage == null)
            {
                return;
            }

            if (stage.SpawnEntries != null)
            {
                foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
                {
                    if (!TryGetLoot(entry.MonsterPrefab, out MonsterLootSO loot))
                    {
                        continue;
                    }

                    totalMin += entry.Count * loot.MinGold;
                    totalMax += entry.Count * loot.MaxGold;
                }
            }

            if (stage.TacticEntries != null)
            {
                foreach (TacticSpawnEntry entry in stage.TacticEntries)
                {
                    int pairCount = Mathf.Max(entry.TotalUnitCount / 2, 0);

                    if (TryGetLoot(entry.LeaderPrefab, out MonsterLootSO leaderLoot))
                    {
                        totalMin += pairCount * leaderLoot.MinGold;
                        totalMax += pairCount * leaderLoot.MaxGold;
                    }

                    bool hasFollower = TryGetLoot(entry.FollowerPrefab, out MonsterLootSO followerLoot);
                    bool hasAlternate = TryGetLoot(entry.AlternateFollowerPrefab, out MonsterLootSO alternateLoot);

                    if (hasFollower && hasAlternate)
                    {
                        totalMin += pairCount * Mathf.Min(followerLoot.MinGold, alternateLoot.MinGold);
                        totalMax += pairCount * Mathf.Max(followerLoot.MaxGold, alternateLoot.MaxGold);
                    }
                    else if (hasFollower)
                    {
                        totalMin += pairCount * followerLoot.MinGold;
                        totalMax += pairCount * followerLoot.MaxGold;
                    }
                }
            }
        }

        private static bool TryGetLoot(GameObject prefab, out MonsterLootSO loot)
        {
            loot = null;
            return prefab != null && prefab.TryGetComponent(out MonsterLootProvider provider) && (loot = provider.Loot) != null;
        }
    }
}
