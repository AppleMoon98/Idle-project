using System;
using System.Collections.Generic;
using Enhancement;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// EquipmentPossessionStatsChangedEvent(장비 보유 효과)가 넘겨주는 값을 RuntimeStats에 반영하는
    /// 전용 매핑 테이블. RuntimeStatApplier(강화/장착 시 효과)와 거의 같은 "enum -> 적용 함수" 모양이지만
    /// 의도적으로 별도로 둔다 — 보유 효과는 AttackPower/MaxHealth까지도 "고정값"이 아니라
    /// "원본 스탯 대비 %"로 취급해야 해서(예: 무기 보유 시 공격력 1% 증가), 그 두 타입만큼은
    /// RuntimeStatApplier의 기존 의미(고정 가산)와 다르다. AttackSpeed/MoveSpeed는 이미 "원본 대비
    /// %"라 기존과 동일하고, CriticalDamage는 강화 시스템과 동일하게 배율 자체에 대한 가산(%p)이다.
    /// CriticalChance는 보유 효과 대상 슬롯이 없어 매핑하지 않는다.
    /// </summary>
    internal static class PossessionStatApplier
    {
        /// <summary>
        /// RuntimeStatApplier.MinAttackInterval과 동일한 구조적 하한선.
        /// </summary>
        private const float MinAttackInterval = 0.05f;

        private static readonly Dictionary<EnhancementStatType, Action<RuntimeStats, CharacterStatsSO, float>> Appliers = new()
        {
            { EnhancementStatType.AttackPower, (stats, baseStats, percent) => stats.AttackPower += baseStats.AttackPower * percent },
            { EnhancementStatType.MaxHealth, (stats, baseStats, percent) => stats.MaxHealth += baseStats.MaxHealth * percent },
            { EnhancementStatType.MoveSpeed, (stats, baseStats, percent) => stats.MoveSpeed += baseStats.MoveSpeed * percent },
            { EnhancementStatType.AttackSpeed, (stats, baseStats, percent) => stats.AttackInterval = Mathf.Max(MinAttackInterval, stats.AttackInterval - baseStats.AttackInterval * percent) },
            { EnhancementStatType.CriticalDamage, (stats, baseStats, percent) => stats.CriticalDamageMultiplier += percent },
        };

        /// <summary>
        /// statType에 대응하는 RuntimeStats 필드에 percent(원본 대비 비율, 예: 0.01 = 1%)를 반영한다.
        /// 매핑이 없는 스탯 타입이면 아무 일도 하지 않는다.
        /// </summary>
        public static void Apply(RuntimeStats stats, CharacterStatsSO baseStats, EnhancementStatType statType, float percent)
        {
            if (Appliers.TryGetValue(statType, out Action<RuntimeStats, CharacterStatsSO, float> apply))
            {
                apply(stats, baseStats, percent);
            }
        }
    }
}
