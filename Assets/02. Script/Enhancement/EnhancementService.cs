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
    }
}
