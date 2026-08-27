using System;
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
        /// 이 능력치의 해금 조건(다른 능력치의 레벨)을 만족했는지 여부. 조건이 없으면 항상 true.
        /// </summary>
        public bool IsUnlocked(EnhancementStatType statType)
        {
            EnhancementConfigSO config = _configs[statType];

            return !config.HasUnlockRequirement || GetLevel(config.RequiredStatType) >= config.RequiredLevel;
        }

        /// <summary>
        /// 이 능력치가 다른 능력치의 레벨을 조건으로 하는 해금 대상인지. UI가 어느 StatEnhancedEvent에
        /// 반응해 잠금 상태를 다시 확인해야 하는지 판단하는 데 쓴다(RequiredStatType은 조건이 없을
        /// 때도 항상 기본값을 반환하므로, 이 값을 먼저 확인해야 한다).
        /// </summary>
        public bool HasUnlockRequirement(EnhancementStatType statType)
        {
            return _configs[statType].HasUnlockRequirement;
        }

        /// <summary>
        /// 이 능력치의 해금 조건이 되는 능력치. IsUnlocked가 false일 때 UI가 잠금 문구를 구성하는 데 쓴다.
        /// </summary>
        public EnhancementStatType GetRequiredStatType(EnhancementStatType statType)
        {
            return _configs[statType].RequiredStatType;
        }

        /// <summary>
        /// 이 능력치의 해금에 필요한 RequiredStatType 레벨.
        /// </summary>
        public int GetRequiredLevel(EnhancementStatType statType)
        {
            return _configs[statType].RequiredLevel;
        }

        /// <summary>
        /// 다음 강화에 필요한 비용(복리 증가: BaseCost * CostMultiplier^레벨). 이미 최대 레벨이면 -1을 반환한다.
        /// BigNumber는 가수+지수 구조라 int/long과 달리 오버플로우로 값이 뒤집힐 여지가 없으므로(과거 int
        /// 캐스팅 오버플로우로 비용이 음수가 되어 TrySpendGold가 "충분하다"고 착각하던 버그의 원인 자체가
        /// 사라진다), saturate 처리는 필요 없다. 다만 반환 직전 TruncateToDisplayPrecision을 거쳐,
        /// StatRowUI가 KoreanNumberFormatter로 보여주는 자리 아래는 실제 차감액에서도 버려진다.
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

        /// <summary>
        /// 구간별로 강화 1회당 비용 증가폭이 달라지는 계단식 누적 비용. 실제 계산은
        /// CostIncrementTier.CalculateTotal(레벨/증가폭 자체는 유한하므로 long 누산으로 충분,
        /// BigNumber로의 승격은 반환 시점의 암시적 변환)이 담당한다.
        /// </summary>
        private static BigNumber CalculateTieredCost(EnhancementConfigSO config, int level)
        {
            return CostIncrementTier.CalculateTotal(config.BaseCost, config.CostIncrementTiers, level);
        }

        /// <summary>
        /// 저장된 레벨로 복원한다. 골드 소모 없이 레벨만 맞추고, 그동안 쌓인 누적 보너스를
        /// StatEnhancedEvent로 재발행해 구독자(Character.StatEnhancementReceiver)가 반영하게 한다.
        /// GameBootstrapper.Start()에서(구독자의 OnEnable이 모두 끝난 뒤) 호출해야 이벤트를 놓치지 않는다.
        ///
        /// GitHub 이슈 #50 - level을 [0, config.MaxLevel]로 클램프한 뒤 항상 적용·재발행한다.
        /// 예전에는 두 가지 문제가 있었다: ① 상한 검증이 없어 손상된 저장값(예: int.MaxValue)이
        /// 그대로 레벨이 됐다 ② level&lt;=0이면 조용히 아무것도 안 해, 이전에 이미 설정된 레벨이
        /// 있어도(같은 서비스 인스턴스에 두 번째로 복원되는 경우) 지워지지 않았다 - 0도 유효한
        /// 복원 대상(레벨 없음)이므로 이제 항상 반영한다. config 자체가 없으면(콘텐츠 갭) 여전히
        /// 아무것도 하지 않는다 - 클램프할 기준이 없기 때문.
        /// </summary>
        public LevelRestoreOutcome RestoreLevel(EnhancementStatType statType, int level)
        {
            if (!_configs.TryGetValue(statType, out EnhancementConfigSO config))
            {
                return LevelRestoreOutcome.ConfigMissing;
            }

            int clampedLevel = Math.Clamp(level, 0, config.MaxLevel);

            _levels[statType] = clampedLevel;
            _events.Publish(new StatEnhancedEvent(statType, config.ValuePerLevel * clampedLevel, clampedLevel));

            return clampedLevel == level ? LevelRestoreOutcome.Applied : LevelRestoreOutcome.ClampedToMax;
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
