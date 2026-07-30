using System.Collections.Generic;
using Core;
using Enhancement.Events;
using Loot;

namespace Enhancement
{
    /// <summary>
    /// 골드를 소모해 능력치를 강화한다. 레벨/비용만 관리하고, 실제 능력치 적용은
    /// StatEnhancedEvent 구독자(Character.StatEnhancementReceiver)가 담당한다.
    /// </summary>
    public sealed class EnhancementService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly CurrencyService _currency;
        private readonly Dictionary<EnhancementStatType, EnhancementConfigSO> _configs = new();
        private readonly Dictionary<EnhancementStatType, int> _levels = new();

        public EnhancementService(EventBus events, CurrencyService currency, EnhancementConfigSO[] configs)
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
        /// 다음 강화에 필요한 비용. 이미 최대 레벨이면 -1을 반환한다.
        /// </summary>
        public int GetNextCost(EnhancementStatType statType)
        {
            EnhancementConfigSO config = _configs[statType];
            int level = GetLevel(statType);

            if (level >= config.MaxLevel)
            {
                return -1;
            }

            return config.BaseCost + config.CostIncreasePerLevel * level;
        }

        /// <summary>
        /// 강화를 시도한다. 최대 레벨이거나 골드가 부족하면 실패한다.
        /// </summary>
        public bool TryEnhance(EnhancementStatType statType)
        {
            int cost = GetNextCost(statType);

            if (cost < 0 || !_currency.TrySpendGold(cost))
            {
                return false;
            }

            int newLevel = GetLevel(statType) + 1;
            _levels[statType] = newLevel;

            EnhancementConfigSO config = _configs[statType];
            _events.Publish(new StatEnhancedEvent(statType, config.ValuePerLevel, newLevel));

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
