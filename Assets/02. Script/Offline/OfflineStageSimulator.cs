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
        /// GitHub 이슈 #27 - Stage.MonsterSpawner는 spawnWithTactics 여부와 무관하게 웨이브 전체를
        /// 항상 한 틱에 즉시 스폰한다(MonsterSpawnEntry.SpawnInterval은 죽은 필드). 예전엔 여기서
        /// entry.Count * entry.SpawnInterval의 합을 "총 스폰 소요시간"으로 삼아 처치 속도의
        /// 별도 병목(killRateBySpawn)으로 취급했는데, 그게 실전 동작과 어긋난 시간 모델이었다 -
        /// 스폰이 순간적이면 스폰 자체가 처치 속도를 제한할 이유가 없으므로, 병목은 오직 DPS
        /// 대비 몬스터 체력(killRateByDamage)뿐이다.
        /// </summary>
        public Result Simulate(StageSO repeatStage, float totalDps, float budget)
        {
            float healthMultiplier = _difficultyConfig != null ? _difficultyConfig.GetMultiplier(_catalog.IndexOf(repeatStage)) : 1f;

            if (!TryBuildStageInfo(repeatStage, healthMultiplier, out int totalMonsterCount, out float averageMonsterHealth))
            {
                return Result.Failed;
            }

            float effectiveKillRate = totalDps / averageMonsterHealth;

            if (effectiveKillRate <= 0f)
            {
                return Result.Failed;
            }

            float timeToClear = totalMonsterCount / effectiveKillRate;
            int timesCleared = Mathf.FloorToInt(budget / timeToClear);
            float leftoverBudget = budget - timesCleared * timeToClear;
            int leftoverMonsters = Mathf.FloorToInt(leftoverBudget * effectiveKillRate);
            int totalMonstersKilled = timesCleared * totalMonsterCount + leftoverMonsters;

            int rewardedKills = Mathf.RoundToInt(totalMonstersKilled * _rewardMultiplier);
            float goldMultiplier = _difficultyConfig != null ? _difficultyConfig.GetGoldMultiplier(_catalog.IndexOf(repeatStage)) : 1f;

            BigNumber totalGold = BigNumber.Zero;
            var equipmentEarned = new List<EquipmentSO>();
            RollLoot(repeatStage, totalMonsterCount, rewardedKills, goldMultiplier, ref totalGold, equipmentEarned);

            return Result.Of(totalGold, equipmentEarned, totalMonstersKilled, timesCleared);
        }

        /// <summary>
        /// 스테이지의 총 몬스터 수/가중 평균 체력을 계산한다. healthMultiplier는
        /// StageDifficultyConfigSO가 실전투에서 StageMonsterScaler로 적용하는 것과 동일한 스테이지별
        /// 배율이다. 몬스터가 하나도 없으면 false. GitHub 이슈 #27 - 총 스폰 소요시간은 더 이상
        /// 계산하지 않는다(MonsterSpawner가 항상 즉시 스폰하므로 실전에 대응하는 값 자체가 없다) -
        /// 예전엔 이 값이 0이면(SpawnInterval을 전부 0으로 채운 스테이지) 몬스터가 있어도
        /// 시뮬레이션 자체가 통째로 실패했는데, 이제 그 조건 자체가 없어져 재현되지 않는다.
        /// </summary>
        private static bool TryBuildStageInfo(StageSO stage, float healthMultiplier, out int totalMonsterCount, out float averageMonsterHealth)
        {
            totalMonsterCount = 0;
            float weightedHealth = 0f;

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                if (entry.MonsterPrefab == null || !entry.MonsterPrefab.TryGetComponent(out CharacterStatsProvider statsProvider))
                {
                    continue;
                }

                totalMonsterCount += entry.Count;
                weightedHealth += entry.Count * statsProvider.Stats.MaxHealth * healthMultiplier;
            }

            averageMonsterHealth = totalMonsterCount > 0 ? weightedHealth / totalMonsterCount : 0f;

            return totalMonsterCount > 0;
        }

        /// <summary>
        /// 스테이지의 스폰 엔트리 비율대로 monstersKilled마리를 배분해, 골드는 각 몬스터 종류의
        /// MonsterLootSO로, 장비는 스테이지의 드롭 테이블로 실제 처치와 동일한 확률로 굴려 누적한다.
        /// monstersKilled가 totalMonsterCount보다 커도(여러 번 반복 클리어한 합계) 비율 배분은
        /// 그대로 성립한다.
        /// </summary>
        private static void RollLoot(StageSO stage, int totalMonsterCount, int monstersKilled, float goldMultiplier, ref BigNumber totalGold, List<EquipmentSO> equipmentEarned)
        {
            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                if (entry.MonsterPrefab == null || !entry.MonsterPrefab.TryGetComponent(out MonsterLootProvider provider) || provider.Loot == null)
                {
                    continue;
                }

                int killsForEntry = Mathf.RoundToInt((float)entry.Count / totalMonsterCount * monstersKilled);

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
