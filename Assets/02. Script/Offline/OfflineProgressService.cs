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
    /// 마지막 저장 시각 대비 경과 시간을 근사 전투 공식으로 시뮬레이션해 골드/장비/스테이지
    /// 진행을 오프라인 보상으로 계산하고 적용한다. 프레임 단위 실시간 재생이 아닌 근사치이며,
    /// 오프라인 중 플레이어 사망/스테이지 실패는 고려하지 않는다.
    /// </summary>
    public sealed class OfflineProgressService
    {
        private readonly EventBus _events;
        private readonly SaveService _saveService;
        private readonly StageCatalogSO _catalog;
        private readonly CharacterStatsSO _playerStats;
        private readonly CharacterStatsSO _soldierStats;
        private readonly int _soldierCount;
        private readonly float _maxOfflineSeconds;

        public OfflineProgressService(
            EventBus events,
            SaveService saveService,
            StageCatalogSO catalog,
            CharacterStatsSO playerStats,
            CharacterStatsSO soldierStats,
            int soldierCount,
            float maxOfflineSeconds)
        {
            _events = events;
            _saveService = saveService;
            _catalog = catalog;
            _playerStats = playerStats;
            _soldierStats = soldierStats;
            _soldierCount = soldierCount;
            _maxOfflineSeconds = maxOfflineSeconds;
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

            StageSO currentStage = _catalog.Find(save.Chapter, save.StageNumber);

            if (currentStage == null)
            {
                return;
            }

            float totalDps = _playerStats.AttackPower / _playerStats.AttackInterval
                + _soldierCount * (_soldierStats.AttackPower / _soldierStats.AttackInterval);

            int totalGold = 0;
            int totalMonstersKilled = 0;
            int stagesCleared = 0;
            var equipmentEarned = new List<EquipmentSO>();

            while (budget > 0f && currentStage != null)
            {
                if (!TryBuildStageInfo(currentStage, out int totalMonsterCount, out float totalSpawnDuration, out float averageMonsterHealth))
                {
                    currentStage = _catalog.GetNext(currentStage);
                    continue;
                }

                float killRateByDamage = totalDps / averageMonsterHealth;
                float killRateBySpawn = totalMonsterCount / totalSpawnDuration;
                float effectiveKillRate = Mathf.Min(killRateByDamage, killRateBySpawn);

                if (effectiveKillRate <= 0f)
                {
                    break;
                }

                float timeToClear = totalMonsterCount / effectiveKillRate;
                bool cleared = timeToClear <= budget;
                int monstersThisStage = cleared ? totalMonsterCount : Mathf.FloorToInt(budget * effectiveKillRate);

                budget = cleared ? budget - timeToClear : 0f;

                RollLoot(currentStage, totalMonsterCount, monstersThisStage, ref totalGold, equipmentEarned);
                totalMonstersKilled += monstersThisStage;

                if (!cleared)
                {
                    break;
                }

                stagesCleared++;
                StageSO next = _catalog.GetNext(currentStage);

                if (next == null)
                {
                    break;
                }

                currentStage = next;
            }

            if (totalGold > 0)
            {
                _events.Publish(new GoldEarnedEvent(totalGold));
            }

            foreach (EquipmentSO equipment in equipmentEarned)
            {
                _events.Publish(new ItemDroppedEvent(equipment));
            }

            // SaveService가 이 이벤트를 구독해 즉시 저장하므로, StageController가 곧이어
            // Start()에서 SaveService.Load()를 읽을 때 오프라인 중 진행된 스테이지를 그대로 이어받는다.
            _events.Publish(new StageChangedEvent(currentStage.Chapter, currentStage.StageNumber));

            _events.Publish(new OfflineProgressCalculatedEvent(
                Mathf.Min(elapsedSeconds, _maxOfflineSeconds),
                totalGold,
                equipmentEarned,
                totalMonstersKilled,
                stagesCleared,
                currentStage.Chapter,
                currentStage.StageNumber));
        }

        /// <summary>
        /// 스테이지의 총 몬스터 수/총 스폰 소요시간/가중 평균 체력을 계산한다.
        /// 몬스터가 하나도 없으면 false.
        /// </summary>
        private static bool TryBuildStageInfo(StageSO stage, out int totalMonsterCount, out float totalSpawnDuration, out float averageMonsterHealth)
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
                weightedHealth += entry.Count * statsProvider.Stats.MaxHealth;
            }

            averageMonsterHealth = totalMonsterCount > 0 ? weightedHealth / totalMonsterCount : 0f;

            return totalMonsterCount > 0 && totalSpawnDuration > 0f;
        }

        /// <summary>
        /// 스테이지의 스폰 엔트리 비율대로 monstersKilled마리를 배분해, 각 몬스터 종류의
        /// MonsterLootSO를 실제 처치와 동일한 확률로 굴려 골드/장비를 누적한다.
        /// </summary>
        private static void RollLoot(StageSO stage, int totalMonsterCount, int monstersKilled, ref int totalGold, List<EquipmentSO> equipmentEarned)
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
                    int? gold = LootRoller.RollGold(provider.Loot);

                    if (gold.HasValue)
                    {
                        totalGold += gold.Value;
                    }

                    equipmentEarned.AddRange(LootRoller.RollEquipment(provider.Loot));
                }
            }
        }
    }
}
