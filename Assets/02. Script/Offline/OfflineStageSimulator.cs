using System.Collections.Generic;
using Character;
using Core;
using Equipment;
using Loot;
using Save;
using Stage;
using UnityEngine;

namespace Offline
{
    /// <summary>
    /// 오프라인 예산(초) 동안 특정 스테이지를 반복 클리어했다고 가정했을 때의 결과(골드/장비/
    /// 처치 수/클리어 횟수)를 근사 시뮬레이션한다. "몇 시간이 지났나"/"지금 전투력이 얼마인가"는
    /// 전혀 모르는 순수 시뮬레이터 — Offline.OfflineProgressService(오케스트레이션)와
    /// Offline.OfflineCombatPowerCalculator(전투력 계산)에서 넘겨받은 값만 갖고 계산한다.
    /// 새로운 스테이지로 "돌파"하지는 않고 역대 최고 기록 스테이지를 그대로 반복하는 이유는
    /// OfflineProgressService 클래스 doc 참고.
    /// </summary>
    public sealed class OfflineStageSimulator
    {
        /// <summary>
        /// Simulate()의 결과. Success가 false면 나머지 필드는 의미 없다(스테이지에 몬스터가
        /// 없거나 처치 속도가 0 이하인 등 시뮬레이션 자체가 성립하지 않는 경우).
        /// </summary>
        public readonly struct Result
        {
            public bool Success { get; }
            public BigNumber TotalGold { get; }
            public IReadOnlyList<EquipmentSO> EquipmentEarned { get; }
            public int TotalMonstersKilled { get; }
            public int TimesCleared { get; }

            private Result(bool success, BigNumber totalGold, IReadOnlyList<EquipmentSO> equipmentEarned, int totalMonstersKilled, int timesCleared)
            {
                Success = success;
                TotalGold = totalGold;
                EquipmentEarned = equipmentEarned;
                TotalMonstersKilled = totalMonstersKilled;
                TimesCleared = timesCleared;
            }

            public static Result Failed { get; } = new Result(false, BigNumber.Zero, System.Array.Empty<EquipmentSO>(), 0, 0);

            public static Result Of(BigNumber totalGold, IReadOnlyList<EquipmentSO> equipmentEarned, int totalMonstersKilled, int timesCleared)
            {
                return new Result(true, totalGold, equipmentEarned, totalMonstersKilled, timesCleared);
            }
        }

        /// <summary>
        /// 일반 웨이브(MonsterSpawnEntry) 한 항목 또는 전술 웨이브(TacticSpawnEntry)의 리더/추종자/
        /// 대체 추종자 한 갈래를 "골드/체력을 어느 프리팹 기준으로 계산할지 + 몇 마리분인지"로
        /// 통일해서 표현한 것 - TryBuildStageInfo(체력 가중평균)와 RollLoot(보상 굴리기)가 이
        /// 하나의 목록만 공유해서 쓰게 하는 것이 GitHub 이슈 #33의 핵심 요구다. Count는 정수일
        /// 필요가 없다 - 전술 추종자가 AlternateFollowerPrefab과 확률로 나뉘면 그 기대값만큼
        /// 소수로 쪼개진 두 그룹이 된다(합산하면 항상 정수 pairCount로 되돌아온다).
        /// </summary>
        private readonly struct EffectiveSpawnGroup
        {
            public readonly GameObject LootPrefab;
            public readonly float Count;
            public readonly float Health;

            public EffectiveSpawnGroup(GameObject lootPrefab, float count, float health)
            {
                LootPrefab = lootPrefab;
                Count = count;
                Health = health;
            }
        }

        private readonly StageCatalogSO _catalog;
        private readonly StageDifficultyConfigSO _difficultyConfig;
        private readonly float _rewardMultiplier;

        public OfflineStageSimulator(StageCatalogSO catalog, StageDifficultyConfigSO difficultyConfig, float rewardMultiplier)
        {
            _catalog = catalog;
            _difficultyConfig = difficultyConfig;
            _rewardMultiplier = rewardMultiplier;
        }

        /// <summary>
        /// 오프라인 동안 반복할 스테이지를 정한다 — 역대 최고 기록 스테이지 그 자체(돌파하지 않는다).
        /// 기록이 아예 없으면(최초 실행) 첫 스테이지부터.
        /// </summary>
        public StageSO ResolveRepeatStage(SaveData save)
        {
            StageSO highestStage = _catalog.Find(save.HighestClearedChapter, save.HighestClearedStageNumber);

            if (highestStage != null)
            {
                return highestStage;
            }

            return _catalog.Stages != null && _catalog.Stages.Length > 0 ? _catalog.Stages[0] : null;
        }

        /// <summary>
        /// repeatStage를 totalDps로 budget초 동안 반복 클리어했다고 가정하고 결과를 계산한다.
        /// 시뮬레이션으로 산출된 처치 마릿수(TotalMonstersKilled, 팝업에 표시되는 값)는 그대로 두고,
        /// 실제 골드/장비 드롭 굴리기에 넣는 마릿수만 rewardMultiplier를 곱해 줄인다 — 골드와
        /// 장비가 항상 같은 비율로 함께 줄어들도록(따로 계수를 두지 않고) 하기 위함.
        ///
        /// GitHub 이슈 #27 - Stage.MonsterSpawner는 spawnWithTactics 여부와 무관하게 일반 웨이브
        /// 전체를 항상 한 틱에 즉시 스폰한다(MonsterSpawnEntry.SpawnInterval은 죽은 필드) - 그래서
        /// 일반 웨이브 자체는 처치 속도의 병목이 아니다. 하지만 전술 웨이브(TacticSpawnEntry)는
        /// 다르다 - TickTactics가 실제로 PairSpawnInterval만큼 시간을 들여 쌍을 하나씩 스폰하고,
        /// 대형이 다 갖춰진 뒤에도 ImmediateEntryDelay만큼 기다렸다가 spawnWithTactics 웨이브가
        /// 합류한다(GitHub 이슈 #33) - 둘 다 CalculateTacticSpawnDelay로 계산해 매 클리어 사이클마다
        /// "처치가 전혀 시작되지 않는 준비 시간"으로 timeToClear에 더한다.
        /// </summary>
        public Result Simulate(StageSO repeatStage, float totalDps, float budget)
        {
            float healthMultiplier = _difficultyConfig != null ? _difficultyConfig.GetMultiplier(_catalog.IndexOf(repeatStage)) : 1f;

            List<EffectiveSpawnGroup> groups = BuildEffectiveSpawnGroups(repeatStage);

            if (!TryBuildStageInfo(groups, healthMultiplier, out int totalMonsterCount, out float averageMonsterHealth))
            {
                return Result.Failed;
            }

            float effectiveKillRate = totalDps / averageMonsterHealth;

            if (effectiveKillRate <= 0f)
            {
                return Result.Failed;
            }

            float spawnDelay = CalculateTacticSpawnDelay(repeatStage);
            float timeToClear = spawnDelay + totalMonsterCount / effectiveKillRate;
            int timesCleared = Mathf.FloorToInt(budget / timeToClear);
            float leftoverBudget = budget - timesCleared * timeToClear;
            float leftoverEffectiveTime = Mathf.Max(leftoverBudget - spawnDelay, 0f);
            int leftoverMonsters = Mathf.FloorToInt(leftoverEffectiveTime * effectiveKillRate);
            int totalMonstersKilled = timesCleared * totalMonsterCount + leftoverMonsters;

            int rewardedKills = Mathf.RoundToInt(totalMonstersKilled * _rewardMultiplier);
            float goldMultiplier = _difficultyConfig != null ? _difficultyConfig.GetGoldMultiplier(_catalog.IndexOf(repeatStage)) : 1f;

            BigNumber totalGold = BigNumber.Zero;
            var equipmentEarned = new List<EquipmentSO>();
            RollLoot(repeatStage, groups, totalMonsterCount, rewardedKills, goldMultiplier, ref totalGold, equipmentEarned);

            return Result.Of(totalGold, equipmentEarned, totalMonstersKilled, timesCleared);
        }

        /// <summary>
        /// 일반 웨이브(SpawnEntries)와 전술 웨이브(TacticEntries)를 하나의 "유효 스폰 구성"
        /// 목록으로 합친다 - Stage.StageProgressTracker.CalculateTotal(실전 클리어 판정)이 세는
        /// 마릿수와 정확히 같은 모집단이 되도록 TacticSpawnEntry.PairCount(GitHub 이슈 #33 - 세
        /// 시스템이 공유하는 단일 진실 공급원)를 그대로 쓴다. 전술 항목의 리더는 항상
        /// LeaderPrefab 1종으로, 추종자는 AlternateFollowerPrefab이 있으면 실전과 동일한 확률
        /// (MonsterSpawner.SpawnFormationPair의 Random.value &lt; AlternateFollowerChance)로
        /// FollowerPrefab/AlternateFollowerPrefab 사이를 확률 가중 기대값으로 나눈 두 그룹으로
        /// 쪼갠다(결정적 근사 - 실제 RNG를 굴리지 않고 기대값을 바로 계산). AlternateFollowerPrefab이
        /// null이면(실전 SpawnFormationPair와 동일하게) AlternateFollowerChance 값과 무관하게
        /// 전량 FollowerPrefab으로 간다. 프리팹이 null이거나 CharacterStatsProvider가 없는
        /// 항목/그룹은 통째로 건너뛴다 - 일반 항목이 이미 그렇게 하던 기존 방어적 관례와 동일
        /// (콘텐츠 오류로 인한 결측을 조용히 무시, StageProgressTracker의 실제 마릿수와는 이
        /// 경우에 한해 어긋날 수 있지만 애초에 콘텐츠 자체가 깨진 경우라 실전에서도 스폰 자체가
        /// 실패한다).
        /// </summary>
        private static List<EffectiveSpawnGroup> BuildEffectiveSpawnGroups(StageSO stage)
        {
            var groups = new List<EffectiveSpawnGroup>();

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                if (TryGetEffectiveHealth(entry.MonsterPrefab, out float health))
                {
                    groups.Add(new EffectiveSpawnGroup(entry.MonsterPrefab, entry.Count, health));
                }
            }

            if (stage.TacticEntries != null)
            {
                foreach (TacticSpawnEntry tacticEntry in stage.TacticEntries)
                {
                    int pairCount = tacticEntry.PairCount;

                    if (pairCount <= 0)
                    {
                        continue;
                    }

                    if (TryGetEffectiveHealth(tacticEntry.LeaderPrefab, out float leaderHealth))
                    {
                        groups.Add(new EffectiveSpawnGroup(tacticEntry.LeaderPrefab, pairCount, leaderHealth));
                    }

                    bool hasAlternate = tacticEntry.AlternateFollowerPrefab != null;
                    float alternateShare = hasAlternate ? Mathf.Clamp01(tacticEntry.AlternateFollowerChance) : 0f;
                    float followerShare = 1f - alternateShare;

                    if (followerShare > 0f && TryGetEffectiveHealth(tacticEntry.FollowerPrefab, out float followerHealth))
                    {
                        groups.Add(new EffectiveSpawnGroup(tacticEntry.FollowerPrefab, pairCount * followerShare, followerHealth));
                    }

                    if (alternateShare > 0f && TryGetEffectiveHealth(tacticEntry.AlternateFollowerPrefab, out float alternateHealth))
                    {
                        groups.Add(new EffectiveSpawnGroup(tacticEntry.AlternateFollowerPrefab, pairCount * alternateShare, alternateHealth));
                    }
                }
            }

            return groups;
        }

        /// <summary>
        /// prefab의 CharacterStatsProvider.BaseStats.MaxHealth를 읽는다. Character.ShieldGuard가
        /// 붙어있으면(방패병 등) 방패가 제공하는 실질 유효 체력(MaxHealth × (1 +
        /// ShieldHealthMultiplier))까지 더한 값을 돌려준다 - 방패를 무시하면 방패병을 실제보다
        /// 훨씬 약하게 계산해 오프라인 보상이 과대 산정된다(사용자 확인, GitHub 이슈 #33).
        /// 다른 방어 특성(예: Knight의 DamageReductionPercent)은 일반 몬스터 계산도 이미 무시하고
        /// 있어 이 계산에서도 동일하게 무시한다 - 이 시뮬레이터 전체의 기존 근사 수준과 일치시키기
        /// 위한 의도적 범위 제한.
        /// </summary>
        private static bool TryGetEffectiveHealth(GameObject prefab, out float health)
        {
            health = 0f;

            if (prefab == null || !prefab.TryGetComponent(out CharacterStatsProvider statsProvider))
            {
                return false;
            }

            float baseHealth = statsProvider.Stats.MaxHealth;

            if (prefab.TryGetComponent(out ShieldGuard shieldGuard))
            {
                baseHealth *= 1f + shieldGuard.ShieldHealthMultiplier;
            }

            health = baseHealth;
            return true;
        }

        /// <summary>
        /// 전술 웨이브가 스폰을 마치기까지 걸리는 시간(초) - 각 전술 엔트리의 쌍 스폰 간격 누적
        /// (pairCount × PairSpawnInterval, TickTactics가 실제로 그만큼 시간을 들여 스폰하므로) +
        /// 마지막 전술 엔트리가 끝난 뒤 spawnWithTactics 웨이브가 합류하기까지의 대기 시간
        /// (ImmediateEntryDelay - 마지막 엔트리에서만 의미 있다. MonsterSpawner.FinishTacticEntry가
        /// 정확히 그 시점에만 이 값을 무장하기 때문). 일반 웨이브(SpawnInterval)는 이 계산에서
        /// 제외한다 - MonsterSpawner가 실제로 즉시 스폰하는 죽은 필드이기 때문(GitHub 이슈 #27,
        /// section HI).
        /// </summary>
        private static float CalculateTacticSpawnDelay(StageSO stage)
        {
            if (stage.TacticEntries == null || stage.TacticEntries.Length == 0)
            {
                return 0f;
            }

            float delay = 0f;

            foreach (TacticSpawnEntry entry in stage.TacticEntries)
            {
                delay += entry.PairCount * entry.PairSpawnInterval;
            }

            delay += stage.TacticEntries[stage.TacticEntries.Length - 1].ImmediateEntryDelay;

            return delay;
        }

        /// <summary>
        /// 유효 스폰 구성(groups)의 총 마릿수/가중 평균 체력을 계산한다. healthMultiplier는
        /// StageDifficultyConfigSO가 실전투에서 StageMonsterScaler로 적용하는 것과 동일한 스테이지별
        /// 배율이다. 몬스터가 하나도 없으면 false.
        /// </summary>
        private static bool TryBuildStageInfo(List<EffectiveSpawnGroup> groups, float healthMultiplier, out int totalMonsterCount, out float averageMonsterHealth)
        {
            float totalCount = 0f;
            float weightedHealth = 0f;

            foreach (EffectiveSpawnGroup group in groups)
            {
                totalCount += group.Count;
                weightedHealth += group.Count * group.Health * healthMultiplier;
            }

            totalMonsterCount = Mathf.RoundToInt(totalCount);
            averageMonsterHealth = totalCount > 0f ? weightedHealth / totalCount : 0f;

            return totalMonsterCount > 0;
        }

        /// <summary>
        /// 유효 스폰 구성(groups)의 비율대로 monstersKilled마리를 배분해, 골드는 각 그룹의 프리팹이
        /// 가진 MonsterLootSO로, 장비는 스테이지의 드롭 테이블(전술 유닛 포함 모든 처치가 공유하는
        /// 하나의 테이블 - 실전 Loot.LootDropper.OnCharacterDied도 몬스터 종류와 무관하게 동일한
        /// stage.EquipmentDrops를 굴린다)로 실제 처치와 동일한 확률로 굴려 누적한다. 전술 유닛도
        /// MonsterLootProvider를 가지므로(방패병/창병/궁병 프리팹 확인됨) 골드도 정상적으로
        /// 포함된다(GitHub 이슈 #33 - "전술 유닛 보상 포함/제외 정책이 실전과 일치함"). totalCount는
        /// groups의 Count 합(항상 totalMonsterCount와 일치)을 그대로 다시 계산하지 않고
        /// totalMonsterCount(int)를 그대로 분모로 쓴다 - TryBuildStageInfo가 이미 이걸로
        /// averageMonsterHealth를 계산했으므로 동일한 분모를 계속 재사용해야 두 계산이
        /// 어긋나지 않는다.
        /// </summary>
        private static void RollLoot(StageSO stage, List<EffectiveSpawnGroup> groups, int totalMonsterCount, int monstersKilled, float goldMultiplier, ref BigNumber totalGold, List<EquipmentSO> equipmentEarned)
        {
            foreach (EffectiveSpawnGroup group in groups)
            {
                if (group.LootPrefab == null || !group.LootPrefab.TryGetComponent(out MonsterLootProvider provider) || provider.Loot == null)
                {
                    continue;
                }

                int killsForEntry = Mathf.RoundToInt(group.Count / totalMonsterCount * monstersKilled);

                for (int i = 0; i < killsForEntry; i++)
                {
                    int? gold = LootRoller.RollGold(provider.Loot, goldMultiplier);

                    if (gold.HasValue)
                    {
                        totalGold += gold.Value;
                    }

                    equipmentEarned.AddRange(LootRoller.RollEquipment(stage.EquipmentDrops));
                }
            }
        }
    }
}
