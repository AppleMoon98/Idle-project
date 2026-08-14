using System;
using System.Collections.Generic;
using Character;
using Core;
using Equipment;
using Loot;
using Loot.Events;
using Offline.Events;
using Save;
using Stage;
using Stage.Events;
using UnityEngine;

namespace Offline
{
    /// <summary>
    /// 마지막 저장 시각 대비 경과 시간을 근사 전투 공식으로 시뮬레이션해 골드/장비 획득을
    /// 오프라인 보상으로 계산하고 적용한다. 프레임 단위 실시간 재생이 아닌 근사치이며,
    /// 오프라인 중 플레이어 사망은 고려하지 않는다. 새로운 스테이지로 "돌파"하지는 않고
    /// 역대 최고 기록 스테이지를 그대로 반복(반복 모드)해서 시간만큼 계속 클리어한다 —
    /// 오프라인 시간 동안 검증 안 된 새 스테이지까지 뚫어버리면(플레이어가 실제로는 못 깰
    /// 스테이지까지) 체감상 부정확하고 밸런스도 깨지기 때문에, 이미 증명된 난이도의 스테이지만
    /// 반복해서 안전하게 누적한다. 시뮬레이션으로 산출된 처치 마릿수(totalMonstersKilled, 팝업에
    /// 표시되는 값)는 그대로 두고, 실제 골드/장비 드롭 굴리기에 넣는 마릿수만 rewardMultiplier를
    /// 곱해 줄인다 — 골드와 장비가 항상 같은 비율로 함께 줄어들도록(따로 계수를 두지 않고) 하기 위함.
    /// </summary>
    public sealed class OfflineProgressService
    {
        private readonly EventBus _events;
        private readonly SaveService _saveService;
        private readonly StageCatalogSO _catalog;
        private readonly StageDifficultyConfigSO _difficultyConfig;
        private readonly CharacterStatsSO _playerStats;
        private readonly CharacterStatsSO _soldierStats;
        private readonly int _soldierCount;
        private readonly float _maxOfflineSeconds;
        private readonly float _rewardMultiplier;

        public OfflineProgressService(
            EventBus events,
            SaveService saveService,
            StageCatalogSO catalog,
            StageDifficultyConfigSO difficultyConfig,
            CharacterStatsSO playerStats,
            CharacterStatsSO soldierStats,
            int soldierCount,
            float maxOfflineSeconds,
            float rewardMultiplier)
        {
            _events = events;
            _saveService = saveService;
            _catalog = catalog;
            _difficultyConfig = difficultyConfig;
            _playerStats = playerStats;
            _soldierStats = soldierStats;
            _soldierCount = soldierCount;
            _maxOfflineSeconds = maxOfflineSeconds;
            _rewardMultiplier = rewardMultiplier;
        }

        /// <summary>
        /// 저장된 마지막 접속 시각을 기준으로 오프라인 보상을 계산해 적용하고 결과 이벤트를 발행한다.
        /// 저장 기록이 없거나(최초 실행) 인정 시간이 0 이하이면 아무 것도 하지 않는다.
        /// </summary>
        public void CalculateAndApply()
        {
            SaveData save = _saveService.Load();

            if (save.LastActiveUnixTime <= 0)
            {
                return;
            }

            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float elapsedSeconds = Mathf.Max(0f, nowUnix - save.LastActiveUnixTime);
            float budget = Mathf.Min(elapsedSeconds, _maxOfflineSeconds);

            if (budget <= 0f)
            {
                return;
            }

            StageSO repeatStage = ResolveRepeatStage(save);

            if (repeatStage == null)
            {
                return;
            }

            float totalDps = _playerStats.AttackPower / _playerStats.AttackInterval
                + _soldierCount * (_soldierStats.AttackPower / _soldierStats.AttackInterval);

            float healthMultiplier = _difficultyConfig != null ? _difficultyConfig.GetMultiplier(_catalog.IndexOf(repeatStage)) : 1f;

            if (!TryBuildStageInfo(repeatStage, healthMultiplier, out int totalMonsterCount, out float totalSpawnDuration, out float averageMonsterHealth))
            {
                return;
            }

            float killRateByDamage = totalDps / averageMonsterHealth;
            float killRateBySpawn = totalMonsterCount / totalSpawnDuration;
            float effectiveKillRate = Mathf.Min(killRateByDamage, killRateBySpawn);

            if (effectiveKillRate <= 0f)
            {
                return;
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

            if (totalGold > 0)
            {
                _events.Publish(new GoldEarnedEvent(totalGold));
            }

            foreach (EquipmentSO equipment in equipmentEarned)
            {
                _events.Publish(new ItemDroppedEvent(equipment));
            }

            // 반복 모드이므로 역대 최고 기록 자체는 갱신되지 않는다(HighestStageClearedEvent 발행 없음) —
            // 항상 그 기록 스테이지로 복귀시킨다(사망으로 뒤로 밀려 있던 현재 위치는 무시하고, 오프라인은
            // "죽지 않고 최고 기록을 반복 클리어했다"는 낙관적 가정만 반영한다).
            _events.Publish(new StageChangedEvent(repeatStage.Chapter, repeatStage.StageNumber, isBreakthrough: false));

            _events.Publish(new OfflineProgressCalculatedEvent(
                Mathf.Min(elapsedSeconds, _maxOfflineSeconds),
                totalGold,
                equipmentEarned,
                totalMonstersKilled,
                timesCleared,
                repeatStage.Chapter,
                repeatStage.StageNumber));
        }

        /// <summary>
        /// 오프라인 동안 반복할 스테이지를 정한다 — 역대 최고 기록 스테이지 그 자체(돌파하지 않는다).
        /// 기록이 아예 없으면(최초 실행) 첫 스테이지부터.
        /// </summary>
        private StageSO ResolveRepeatStage(SaveData save)
        {
            StageSO highestStage = _catalog.Find(save.HighestClearedChapter, save.HighestClearedStageNumber);

            if (highestStage != null)
            {
                return highestStage;
            }

            return _catalog.Stages != null && _catalog.Stages.Length > 0 ? _catalog.Stages[0] : null;
        }

        /// <summary>
        /// 스테이지의 총 몬스터 수/총 스폰 소요시간/가중 평균 체력을 계산한다. healthMultiplier는
        /// StageDifficultyConfigSO가 실전투에서 StageMonsterScaler로 적용하는 것과 동일한 스테이지별
        /// 배율이다. 몬스터가 하나도 없으면 false.
        /// </summary>
        private static bool TryBuildStageInfo(StageSO stage, float healthMultiplier, out int totalMonsterCount, out float totalSpawnDuration, out float averageMonsterHealth)
        {
            totalMonsterCount = 0;
            totalSpawnDuration = 0f;
            float weightedHealth = 0f;

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                if (entry.MonsterPrefab == null || !entry.MonsterPrefab.TryGetComponent(out CharacterStatsProvider statsProvider))
                {
                    continue;
                }

                totalMonsterCount += entry.Count;
                totalSpawnDuration += entry.Count * entry.SpawnInterval;
                weightedHealth += entry.Count * statsProvider.Stats.MaxHealth * healthMultiplier;
            }

            averageMonsterHealth = totalMonsterCount > 0 ? weightedHealth / totalMonsterCount : 0f;

            return totalMonsterCount > 0 && totalSpawnDuration > 0f;
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
