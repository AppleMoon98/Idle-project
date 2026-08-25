using Character;
using Enhancement;
using Equipment;
using Rank;
using Skill;
using Soldier;
using SoldierEnhancement;
using UnityEngine;

namespace Offline
{
    /// <summary>
    /// "세이브 복원이 완료된 시점의 유효 전투력 스냅샷"을 실제 DPS 한 값으로 계산한다 —
    /// Offline.OfflineProgressService의 오케스트레이션(경과 시간/스테이지 반복 시뮬레이션)과
    /// 분리된, 순수 "지금 전투력이 얼마인가" 계산 전담 클래스.
    ///
    /// **밸런스 정책 — 이 스냅샷에 포함되는 성장 시스템(전부 세이브에 영구 저장되는 것만):**
    /// 플레이어 강화(Enhancement, AttackPower/AttackSpeed) · 장비 착용 스탯(EquipmentStatService) ·
    /// 장비 보유 효과(EquipmentPossessionService) · 랭크 보너스(RankSO.PlayerStatBonusPercent) ·
    /// 실제로 배치된 병사 각각의 병사 강화(SoldierEnhancement, 전역)와 등급 지분(플레이어 공격력
    /// 대비 %) · 장착되고 자동 발동 켜진 데미지 스킬(AreaDamage/SingleTargetStrike/Poison/Whirlwind/
    /// Meteor)의 데미지. **의도적으로 제외:** MaxHealth/CriticalChance/CriticalDamage(이 DPS 근사
    /// 공식 자체가 안 쓰는 값) · 스킬 버프/디버프/힐(SelfBuff/Debuff/Curse/SoldierBuff/PartyHeal, 세이브에
    /// 영구 저장되지 않는 일시적 실시간 효과라 재접속 시점엔 애초에 걸려있지 않음). 새 성장 시스템을
    /// 추가할 때 오프라인 보상에도 반영하려면 이 정책 목록과 아래 세 메서드를 함께 갱신해야 한다.
    ///
    /// **호출 시점 전제:** 반드시 GameBootstrapper.Start()에서 모든 세이브 복원(Enhancement/Equipment/
    /// Rank의 RestoreLevel/RecomputeAndPublish)이 끝난 뒤에 ComputeTotalDps()를 호출해야 한다 —
    /// 그래야 _playerEnhancement.GetLevel() 등이 실제 세이브된 값을 정확히 반영한 상태다
    /// (Offline.OfflineProgressService.ApplyCapturedReward 참고).
    /// </summary>
    public sealed class OfflineCombatPowerCalculator
    {
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

        public OfflineCombatPowerCalculator(
            CharacterStatsSO playerStats,
            EnhancementService playerEnhancement,
            EquipmentStatService equipmentStatService,
            EquipmentPossessionService equipmentPossessionService,
            SoldierEnhancementService soldierEnhancement,
            SoldierDeploymentService soldierDeployment,
            SoldierGradeConfigSO soldierGradeConfig,
            RankService rankService,
            SkillService skillService,
            SkillLoadoutService skillLoadoutService)
        {
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
        }

        /// <summary>
        /// 플레이어 + 실제 배치된 병사 전원 + 장착된 데미지 스킬을 합산한 현재 유효 DPS.
        /// </summary>
        public float ComputeTotalDps()
        {
            (float playerAttackPower, float playerAttackInterval) = ComputeEffectivePlayerAttackStats();

            return (playerAttackInterval > 0f ? playerAttackPower / playerAttackInterval : 0f)
                + ComputeTotalSoldierDps(playerAttackPower)
                + ComputeTotalSkillDps(playerAttackPower);
        }

        /// <summary>
        /// 플레이어의 실제 최종 공격력/공격주기를 계산한다 — 기본 스탯(CharacterStatsSO) +
        /// 강화(Enhancement) + 장비 착용 스탯(EquipmentStatService) + 장비 보유 효과
        /// (EquipmentPossessionService) + 랭크 보너스 순으로 Character.RuntimeStatApplier/
        /// PossessionStatApplier(둘 다 internal이지만 같은 어셈블리라 접근 가능)를 그대로 재사용해
        /// 실제 런타임과 동일한 공식으로 누적한다.
        /// </summary>
        private (float AttackPower, float AttackInterval) ComputeEffectivePlayerAttackStats()
        {
            var stats = new RuntimeStats(_playerStats);

            if (_playerEnhancement != null)
            {
                ApplyEnhancementLevel(stats, EnhancementStatType.AttackPower);
                ApplyEnhancementLevel(stats, EnhancementStatType.AttackSpeed);
            }

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

        private void ApplyEnhancementLevel(RuntimeStats stats, EnhancementStatType statType)
        {
            int level = _playerEnhancement.GetLevel(statType);

            if (level <= 0)
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
    }
}
