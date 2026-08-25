using System;
using System.Collections.Generic;
using Character;
using Core;
using Enhancement;
using Equipment;
using Loot;
using Loot.Events;
using Offline.Events;
using Rank;
using Save;
using Skill;
using Soldier;
using SoldierEnhancement;
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
        private readonly EnhancementService _playerEnhancement;
        private readonly EquipmentStatService _equipmentStatService;
        private readonly EquipmentPossessionService _equipmentPossessionService;
        private readonly SoldierEnhancementService _soldierEnhancement;
        private readonly SoldierDeploymentService _soldierDeployment;
        private readonly SoldierGradeConfigSO _soldierGradeConfig;
        private readonly RankService _rankService;
        private readonly SkillService _skillService;
        private readonly SkillLoadoutService _skillLoadoutService;
        private readonly float _maxOfflineSeconds;
        private readonly float _rewardMultiplier;

        /// <summary>
        /// 플레이어/병사의 실제 강화·장비·랭크·등급 보너스와, 장착된 스킬의 데미지까지 전부
        /// 반영한 최종 DPS로 오프라인 처치 속도를 계산한다 — 기존에는 인스펙터에 고정된
        /// CharacterStatsSO 원본값과 고정 병사 수만 썼기 때문에, 아무리 강화하고 장비를 맞추고
        /// 병사를 배치해도 오프라인 보상 속도가 전혀 늘지 않는 문제가 있었다. 계산 시점
        /// (GameBootstrapper.Start()의 가장 첫 줄)에는 아직 EnhancementService.RestoreLevel/
        /// RankService.RestoreLevel이 실행되기 전이라(LastActiveUnixTime 버그 회피를 위해 반드시
        /// 이 순서를 지켜야 함, CalculateAndApply 상단 주석 참고) 실제 라이브 RuntimeStats를
        /// 그대로 읽을 수 없다 — 대신 SaveData의 원본 레벨/보유 상태에서 같은 공식(Character.
        /// RuntimeStatApplier/PossessionStatApplier, UI.SoldierDetailPopupUI와 동일한 패턴)을 직접
        /// 재현해 계산한다. 랭크는 RankService.SeedRank가 이미 Awake에서(RestoreLevel보다 먼저)
        /// 이벤트 없이 조용히 세팅해두므로 RankService.CurrentRank를 그대로 읽어도 안전하다.
        /// 스킬 레벨/장착 상태(SkillService/SkillLoadoutService)도 전부 Awake에서 이미 복원이
        /// 끝나 있어 마찬가지로 그대로 읽는다.
        /// </summary>
        public OfflineProgressService(
            EventBus events,
            SaveService saveService,
            StageCatalogSO catalog,
            StageDifficultyConfigSO difficultyConfig,
            CharacterStatsSO playerStats,
            EnhancementService playerEnhancement,
            EquipmentStatService equipmentStatService,
            EquipmentPossessionService equipmentPossessionService,
            SoldierEnhancementService soldierEnhancement,
            SoldierDeploymentService soldierDeployment,
            SoldierGradeConfigSO soldierGradeConfig,
            RankService rankService,
            SkillService skillService,
            SkillLoadoutService skillLoadoutService,
            float maxOfflineSeconds,
            float rewardMultiplier)
        {
            _events = events;
            _saveService = saveService;
            _catalog = catalog;
            _difficultyConfig = difficultyConfig;
            _playerStats = playerStats;
            _playerEnhancement = playerEnhancement;
            _equipmentStatService = equipmentStatService;
            _equipmentPossessionService = equipmentPossessionService;
            _soldierEnhancement = soldierEnhancement;
            _soldierDeployment = soldierDeployment;
            _soldierGradeConfig = soldierGradeConfig;
            _rankService = rankService;
            _skillService = skillService;
            _skillLoadoutService = skillLoadoutService;
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

            (float playerAttackPower, float playerAttackInterval) = ComputeEffectivePlayerAttackStats(save);
            float totalDps = (playerAttackInterval > 0f ? playerAttackPower / playerAttackInterval : 0f)
                + ComputeTotalSoldierDps(playerAttackPower)
                + ComputeTotalSkillDps(playerAttackPower);

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
        /// 플레이어의 실제 최종 공격력/공격주기를 계산한다 — 기본 스탯(CharacterStatsSO) +
        /// 강화(Enhancement, 레벨은 SaveData에서 직접) + 장비 착용 스탯(EquipmentStatService) +
        /// 장비 보유 효과(EquipmentPossessionService) 순으로 Character.RuntimeStatApplier /
        /// PossessionStatApplier(둘 다 internal이지만 같은 어셈블리라 접근 가능)를 그대로 재사용해
        /// 실제 런타임과 동일한 공식으로 누적한다.
        /// </summary>
        private (float AttackPower, float AttackInterval) ComputeEffectivePlayerAttackStats(SaveData save)
        {
            var stats = new RuntimeStats(_playerStats);

            ApplyEnhancementLevel(stats, EnhancementStatType.AttackPower, save.AttackPowerLevel);
            ApplyEnhancementLevel(stats, EnhancementStatType.AttackSpeed, save.AttackSpeedLevel);

            if (_equipmentStatService != null)
            {
                RuntimeStatApplier.Apply(stats, _playerStats, EnhancementStatType.AttackPower, _equipmentStatService.GetCurrentTotal(EnhancementStatType.AttackPower));
                RuntimeStatApplier.Apply(stats, _playerStats, EnhancementStatType.AttackSpeed, _equipmentStatService.GetCurrentTotal(EnhancementStatType.AttackSpeed));
            }

            if (_equipmentPossessionService != null)
            {
                PossessionStatApplier.Apply(stats, _playerStats, EnhancementStatType.AttackPower, _equipmentPossessionService.GetCurrentTotal(EnhancementStatType.AttackPower));
                PossessionStatApplier.Apply(stats, _playerStats, EnhancementStatType.AttackSpeed, _equipmentPossessionService.GetCurrentTotal(EnhancementStatType.AttackSpeed));
            }

            // 랭크 보너스(Character.RankStatReceiver와 동일한 공식) — 기본 스탯 대비 % 가산.
            if (_rankService?.CurrentRank != null)
            {
                PossessionStatApplier.Apply(stats, _playerStats, EnhancementStatType.AttackPower, _rankService.CurrentRank.PlayerStatBonusPercent);
            }

            return (stats.AttackPower, stats.AttackInterval);
        }

        private void ApplyEnhancementLevel(RuntimeStats stats, EnhancementStatType statType, int level)
        {
            if (_playerEnhancement == null || level <= 0)
            {
                return;
            }

            float cumulativeDelta = _playerEnhancement.GetValuePerLevel(statType) * level;
            RuntimeStatApplier.Apply(stats, _playerStats, statType, cumulativeDelta);
        }

        /// <summary>
        /// 현재 실제로 배치된 병사 전원의 DPS 합. 병사마다 자기 자신의 SoldierSO.Prefab 기본
        /// 스탯에서 시작해 병사 강화(전역 누적)와 등급 지분(플레이어 공격력 대비 %)을 얹는다 —
        /// UI.SoldierDetailPopupUI.ComputeStats와 동일한 공식. 배치된 병사가 없으면 0.
        /// </summary>
        private float ComputeTotalSoldierDps(float playerAttackPowerReference)
        {
            if (_soldierDeployment == null)
            {
                return 0f;
            }

            float totalDps = 0f;

            foreach (OwnedSoldier owned in _soldierDeployment.GetDeployedSoldiers())
            {
                CharacterStatsProvider prefabStats = owned.Definition.Prefab != null
                    ? owned.Definition.Prefab.GetComponent<CharacterStatsProvider>()
                    : null;

                if (prefabStats == null || prefabStats.BaseStats == null)
                {
                    continue;
                }

                CharacterStatsSO baseStats = prefabStats.BaseStats;
                var stats = new RuntimeStats(baseStats);

                if (_soldierEnhancement != null)
                {
                    foreach (EnhancementStatType statType in _soldierEnhancement.StatTypes)
                    {
                        float cumulativeDelta = _soldierEnhancement.GetValuePerLevel(statType) * _soldierEnhancement.GetLevel(statType);
                        RuntimeStatApplier.Apply(stats, baseStats, statType, cumulativeDelta);
                    }
                }

                if (owned.Definition.Grade != null && _soldierGradeConfig != null)
                {
                    stats.AttackPower += playerAttackPowerReference * _soldierGradeConfig.GetPercent(owned.Definition.Grade);
                }

                if (stats.AttackInterval > 0f)
                {
                    totalDps += stats.AttackPower / stats.AttackInterval;
                }
            }

            return totalDps;
        }

        /// <summary>
        /// 현재 장착(자동 발동 켜짐)된 6개 스킬 슬롯 중 실제로 피해를 주는 종류(AreaDamage/
        /// SingleTargetStrike/Poison/Whirlwind/Meteor)의 평균 DPS 합. 버프/디버프/힐 계열(SelfBuff/
        /// Debuff/Curse/SoldierBuff/PartyHeal)은 직접 피해를 주지 않아 제외한다. 각 효과의 한 방
        /// 데미지는 Skill.Effects의 실제 공식과 동일하게 (플레이어 공격력 + magnitude)이고, 그
        /// 값을 평균 발동 주기로 나눠 DPS로 환산한다 — AoE로 여러 대상을 동시에 때리는 것(AreaDamage/
        /// Whirlwind/Meteor)은 기존 시뮬레이션 자체가 "평균 몬스터 1체" 기준 스칼라 DPS만 다루므로
        /// (Combat.Attacker 기반 플레이어/병사 DPS도 마찬가지로 단일 대상 기준), 여기서도 다중
        /// 타격을 배수로 반영하지 않는 동일한 근사를 따른다 — Meteor만 예외로, 포탄 자체의 개수
        /// (MeteorShellCount)만큼은 명시적으로 곱한다(포탄 하나하나가 각자 다른 위치에 떨어지는
        /// 별개의 타격이라 "한 번의 캐스트가 여러 번 때린다"는 의미가 AreaDamage/Whirlwind의
        /// "한 번 때릴 때 반경 안 전체가 맞는다"는 것과는 다르기 때문).
        /// </summary>
        private float ComputeTotalSkillDps(float playerAttackPowerReference)
        {
            if (_skillLoadoutService == null || _skillService == null)
            {
                return 0f;
            }

            float totalDps = 0f;

            for (int slotIndex = 0; slotIndex < SkillLoadoutService.SlotCount; slotIndex++)
            {
                if (!_skillLoadoutService.IsEnabled(slotIndex))
                {
                    continue;
                }

                SkillSO definition = _skillLoadoutService.GetEquipped(slotIndex);

                if (definition == null)
                {
                    continue;
                }

                int level = _skillService.GetLevel(definition);

                if (level <= 0)
                {
                    continue;
                }

                float hitDamage = playerAttackPowerReference + definition.GetMagnitude(level);
                float cooldown = Mathf.Max(0.01f, definition.Cooldown);

                switch (definition.EffectType)
                {
                    case SkillEffectType.AreaDamage:
                    case SkillEffectType.SingleTargetStrike:
                        totalDps += hitDamage / cooldown;
                        break;

                    case SkillEffectType.Poison:
                    case SkillEffectType.Whirlwind:
                    {
                        float tickInterval = Mathf.Max(0.1f, definition.TickInterval);
                        float duration = definition.GetBuffDuration(level);
                        totalDps += hitDamage * duration / (tickInterval * cooldown);
                        break;
                    }

                    case SkillEffectType.Meteor:
                        totalDps += definition.MeteorShellCount * hitDamage / cooldown;
                        break;
                }
            }

            return totalDps;
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
