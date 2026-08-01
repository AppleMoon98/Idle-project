using System;
using System.Collections.Generic;
using Enhancement;

namespace Character
{
    /// <summary>
    /// EnhancementStatType별로 RuntimeStats의 어느 필드에 증분을 더할지 정의하는 단일 매핑 테이블.
    /// StatEnhancementReceiver(강화)와 EquipmentStatReceiver(장비)가 이 표 하나를 공유해서,
    /// 새 능력치(공격속도/이동속도/공격범위 등)가 추가될 때 손댈 곳을 한 곳으로 줄인다.
    /// </summary>
    internal static class RuntimeStatApplier
    {
        private static readonly Dictionary<EnhancementStatType, Action<RuntimeStats, float>> Appliers = new()
        {
            { EnhancementStatType.AttackPower, (stats, delta) => stats.AttackPower += delta },
            { EnhancementStatType.MaxHealth, (stats, delta) => stats.MaxHealth += delta },
        };

        /// <summary>
        /// statType에 대응하는 RuntimeStats 필드에 delta를 더한다. 매핑이 없는 스탯 타입이면 아무 일도 하지 않는다.
        /// </summary>
        public static void Apply(RuntimeStats stats, EnhancementStatType statType, float delta)
        {
            if (Appliers.TryGetValue(statType, out Action<RuntimeStats, float> apply))
            {
                apply(stats, delta);
            }
        }
    }
}
