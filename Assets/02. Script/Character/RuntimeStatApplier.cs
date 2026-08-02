using System;
using System.Collections.Generic;
using Enhancement;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// EnhancementStatType별로 RuntimeStats의 어느 필드에 증분을 더할지 정의하는 단일 매핑 테이블.
    /// StatEnhancementReceiver(강화)와 EquipmentStatReceiver(장비)가 이 표 하나를 공유해서,
    /// 새 능력치(공격속도/이동속도/공격범위 등)가 추가될 때 손댈 곳을 한 곳으로 줄인다.
    /// 공격속도/이동속도는 "레벨당 N%"로 기획되어 있어 baseStats(원본 SO)의 값을 기준으로
    /// 퍼센트를 계산한다 — 그래서 다른 스탯들과 달리 원본 스탯도 함께 받는다.
    /// </summary>
    internal static class RuntimeStatApplier
    {
        /// <summary>
        /// 공격 주기가 이 값 밑으로는 내려가지 않게 한다(공격속도 강화가 0 이하로 만들어 공격이
        /// 멈추거나 무한 반복되는 것을 막는 구조적 하한선).
        /// </summary>
        private const float MinAttackInterval = 0.05f;

        private static readonly Dictionary<EnhancementStatType, Action<RuntimeStats, CharacterStatsSO, float>> Appliers = new()
        {
            { EnhancementStatType.AttackPower, (stats, baseStats, delta) => stats.AttackPower += delta },
            { EnhancementStatType.MaxHealth, (stats, baseStats, delta) => stats.MaxHealth += delta },
            { EnhancementStatType.MoveSpeed, (stats, baseStats, delta) => stats.MoveSpeed += baseStats.MoveSpeed * delta },
            { EnhancementStatType.AttackSpeed, (stats, baseStats, delta) => stats.AttackInterval = Mathf.Max(MinAttackInterval, stats.AttackInterval - baseStats.AttackInterval * delta) },
            { EnhancementStatType.CriticalChance, (stats, baseStats, delta) => stats.CriticalChance = Mathf.Clamp01(stats.CriticalChance + delta) },
            { EnhancementStatType.CriticalDamage, (stats, baseStats, delta) => stats.CriticalDamageMultiplier += delta },
        };

        /// <summary>
        /// statType에 대응하는 RuntimeStats 필드에 delta를 반영한다. 공격속도/이동속도는 delta를
        /// baseStats 기준 비율(예: 0.01 = 1%)로 해석하고, 나머지는 delta를 그대로 더한다. 매핑이
        /// 없는 스탯 타입이면 아무 일도 하지 않는다.
        /// </summary>
        public static void Apply(RuntimeStats stats, CharacterStatsSO baseStats, EnhancementStatType statType, float delta)
        {
            if (Appliers.TryGetValue(statType, out Action<RuntimeStats, CharacterStatsSO, float> apply))
            {
                apply(stats, baseStats, delta);
            }
        }
    }
}
