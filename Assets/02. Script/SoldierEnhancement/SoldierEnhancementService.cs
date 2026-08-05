using System;
using System.Collections.Generic;
using Core;
using Enhancement;
using Loot;
using SoldierEnhancement.Events;
using UnityEngine;

namespace SoldierEnhancement
{
    /// <summary>
    /// 골드를 소모해 배치된 모든 병사에게 전역 적용되는 능력치를 강화한다. Enhancement.EnhancementService와
    /// 완전히 동일한 구조의 병렬 서비스다 — 대상이 Player 한 명이 아니라 "병사 전체"라는 점만 다르고,
    /// 병사는 계속 풀링/재스폰되므로 실제 능력치 적용은 스폰마다 자신을 재계산하는
    /// Soldier.SoldierStatReceiver가 담당한다(EnhancementService가 Character.StatEnhancementReceiver에
    /// 위임하는 것과 같은 "서비스는 레벨/비용만, 적용은 구독자가" 분리).
    /// </summary>
    public sealed class SoldierEnhancementService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly CurrencyService _currency;
        private readonly Dictionary<EnhancementStatType, EnhancementConfigSO> _configs = new();
        private readonly Dictionary<EnhancementStatType, int> _levels = new();

        /// <summary>
        /// 현재 설정된(강화 가능한) 능력치 종류 목록. UI가 표시할 행을 결정할 때 사용한다.
        /// </summary>
        public IEnumerable<EnhancementStatType> StatTypes => _configs.Keys;

        public SoldierEnhancementService(EventBus events, CurrencyService currency, EnhancementConfigSO[] configs)
        {
            _events = events;
            _currency = currency;

            foreach (EnhancementConfigSO config in configs)
            {
                _configs[config.StatType] = config;
            }
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 능력치의 현재 강화 레벨.
        /// </summary>
        public int GetLevel(EnhancementStatType statType)
        {
            return _levels.TryGetValue(statType, out int level) ? level : 0;
        }

        /// <summary>
        /// 능력치의 최대 강화 레벨.
        /// </summary>
        public int GetMaxLevel(EnhancementStatType statType)
        {
            return _configs[statType].MaxLevel;
        }

        /// <summary>
        /// 강화 1회당 증가하는 능력치 값.
        /// </summary>
        public float GetValuePerLevel(EnhancementStatType statType)
        {
            return _configs[statType].ValuePerLevel;
        }

        /// <summary>
        /// 다음 강화에 필요한 비용. Enhancement.EnhancementService.GetNextCost와 동일한 계산(계단식/복리
        /// 자동 분기, BigNumber라 saturate 불필요, TruncateToDisplayPrecision으로 화면 표시 자리와
        /// 실제 차감액을 일치시킴)을 그대로 쓴다. 이미 최대 레벨이면 -1을 반환한다.
        /// </summary>
        public BigNumber GetNextCost(EnhancementStatType statType)
        {
            EnhancementConfigSO config = _configs[statType];
            int level = GetLevel(statType);

            if (level >= config.MaxLevel)
            {
                return new BigNumber(-1, 0);
            }

            BigNumber rawCost = config.CostIncrementTiers != null && config.CostIncrementTiers.Count > 0
                ? CalculateTieredCost(config, level)
                : CalculateCompoundCost(config, level);

            // 화면에 보이는 자리수 아래는 실제 차감 비용에서도 버려서, 표시된 숫자가 곧 실제
            // 차감액과 항상 일치하게 한다(BigNumber.TruncateToDisplayPrecision 문서 참고).
            return rawCost.TruncateToDisplayPrecision();
        }

        private static BigNumber CalculateCompoundCost(EnhancementConfigSO config, int level)
        {
            double rawCost = config.BaseCost * Math.Pow(config.CostMultiplier, level);

            return new BigNumber(rawCost, 0);
        }

        private static BigNumber CalculateTieredCost(EnhancementConfigSO config, int level)
        {
            IReadOnlyList<CostIncrementTier> tiers = config.CostIncrementTiers;
            long total = config.BaseCost;

            for (int i = 0; i < tiers.Count; i++)
            {
                int tierStart = tiers[i].LevelThreshold;
                int tierEnd = i + 1 < tiers.Count ? tiers[i + 1].LevelThreshold : int.MaxValue;
                int levelsInTier = Mathf.Max(0, Mathf.Min(level, tierEnd) - tierStart);

                total += (long)levelsInTier * tiers[i].Increment;
            }

            return total;
        }

        /// <summary>
        /// 저장된 레벨로 복원한다. 골드 소모 없이 레벨만 맞춘다. GameBootstrapper.Awake()에서 호출한다 —
        /// Player용과 달리 실제 적용은 이벤트 재생이 아니라 SoldierStatReceiver가 스폰 시점에 직접
        /// 조회하는 방식이라, 어떤 병사가 스폰되기보다 먼저 세팅되기만 하면 되고 Start() 타이밍(다른
        /// 구독자의 OnEnable 대기)을 지킬 필요가 없다.
        /// </summary>
        public void RestoreLevel(EnhancementStatType statType, int level)
        {
            if (level <= 0 || !_configs.TryGetValue(statType, out EnhancementConfigSO config))
            {
                return;
            }

            _levels[statType] = level;
            _events.Publish(new SoldierStatEnhancedEvent(statType, config.ValuePerLevel * level, level));
        }

        /// <summary>
        /// 강화를 시도한다. 최대 레벨이거나 골드가 부족하면 실패한다.
        /// </summary>
        public bool TryEnhance(EnhancementStatType statType)
        {
            BigNumber cost = GetNextCost(statType);

            if (cost < 0 || !_currency.TrySpendGold(cost))
            {
                return false;
            }

            int newLevel = GetLevel(statType) + 1;
            _levels[statType] = newLevel;

            EnhancementConfigSO config = _configs[statType];
            _events.Publish(new SoldierStatEnhancedEvent(statType, config.ValuePerLevel, newLevel));

            return true;
        }

        /// <summary>
        /// 강화를 최대 count회 반복 시도한다. 골드 부족/최대 레벨로 실패하는 즉시 멈춘다.
        /// </summary>
        /// <returns>실제로 성공한 강화 횟수.</returns>
        public int TryEnhanceMultiple(EnhancementStatType statType, int count)
        {
            int succeeded = 0;

            for (int i = 0; i < count; i++)
            {
                if (!TryEnhance(statType))
                {
                    break;
                }

                succeeded++;
            }

            return succeeded;
        }
    }
}
