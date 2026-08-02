using System;
using System.Collections.Generic;
using Core;
using Enhancement.Events;
using Loot;
using UnityEngine;

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

        /// <summary>
        /// 현재 설정된(강화 가능한) 능력치 종류 목록. UI가 표시할 행을 결정할 때 사용한다.
        /// </summary>
        public IEnumerable<EnhancementStatType> StatTypes => _configs.Keys;

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
        /// 다음 강화에 필요한 비용(복리 증가: BaseCost * CostMultiplier^레벨). 이미 최대 레벨이면 -1을 반환한다.
        /// double로 계산 후 int 범위로 saturate한다 — 배율이 1.5~3배씩 복리로 쌓이면 int 범위(약 21억)를
        /// 레벨 50 안팎에서 이미 넘어서는데, int로 그대로 캐스팅하면 오버플로우로 음수가 되어
        /// TrySpendGold가 "비용이 충분하다"고 착각해 강화가 사실상 공짜(심지어 골드가 늘어나는
        /// 방향)로 성립해버리는 문제가 있었다. 실질적으로는 도달 불가능한 레벨이니 int.MaxValue로
        /// 막아두는 것으로 충분하다.
        /// </summary>
        public int GetNextCost(EnhancementStatType statType)
        {
            EnhancementConfigSO config = _configs[statType];
            int level = GetLevel(statType);

            if (level >= config.MaxLevel)
            {
                return -1;
            }

            double rawCost = config.BaseCost * Math.Pow(config.CostMultiplier, level);

            return rawCost >= int.MaxValue ? int.MaxValue : Mathf.RoundToInt((float)rawCost);
        }

        /// <summary>
        /// 저장된 레벨로 복원한다. 골드 소모 없이 레벨만 맞추고, 그동안 쌓인 누적 보너스를
        /// StatEnhancedEvent로 재발행해 구독자(Character.StatEnhancementReceiver)가 반영하게 한다.
        /// GameBootstrapper.Start()에서(구독자의 OnEnable이 모두 끝난 뒤) 호출해야 이벤트를 놓치지 않는다.
        /// </summary>
        public void RestoreLevel(EnhancementStatType statType, int level)
        {
            if (level <= 0 || !_configs.TryGetValue(statType, out EnhancementConfigSO config))
            {
                return;
            }

            _levels[statType] = level;
            _events.Publish(new StatEnhancedEvent(statType, config.ValuePerLevel * level, level));
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
