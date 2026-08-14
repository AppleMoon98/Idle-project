using Character;
using Enhancement;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// "현재 스탯 값 대비 %만큼 올리거나(양수) 내리고(음수), 나중에 정확히 그만큼만 되돌린다"는
    /// 스킬 버프/디버프 공통 로직. Character.PossessionStatApplier(장비 보유 효과, 원본 스탯 대비 %)와
    /// 달리 "현재 값" 기준이다 - 버프/디버프는 서로 다른 스킬이 겹쳐 걸려도 각자 시전 시점의 현재
    /// 값을 기준으로 계산하고 그 델타만 정확히 되돌리면 되므로, 원본(BaseStats) 참조가 필요 없다.
    /// 공격속도만 예외적으로 "값이 작을수록 빠르다"는 AttackInterval의 반대 방향 의미라 부호를
    /// 뒤집어 적용한다(PossessionStatApplier의 AttackSpeed 처리와 동일한 관례).
    /// </summary>
    public static class SkillBuffStatApplier
    {
        private const float MinAttackInterval = 0.05f;

        /// <summary>
        /// stats의 statType 값에 percent만큼(현재 값 대비 비율, 예: 0.1 = +10%, -0.2 = -20%)을
        /// 적용하고 실제로 적용된 델타를 반환한다 - 호출자는 이 델타를 저장해뒀다가 Revert에
        /// 그대로 넘겨야 한다. 지원하지 않는 스탯 타입이면 아무 일도 하지 않고 0을 반환한다.
        /// </summary>
        public static float ApplyPercent(RuntimeStats stats, EnhancementStatType statType, float percent)
        {
            switch (statType)
            {
                case EnhancementStatType.AttackPower:
                {
                    float delta = stats.AttackPower * percent;
                    stats.AttackPower += delta;
                    return delta;
                }
                case EnhancementStatType.MaxHealth:
                {
                    float delta = stats.MaxHealth * percent;
                    stats.MaxHealth += delta;
                    return delta;
                }
                case EnhancementStatType.MoveSpeed:
                {
                    float delta = stats.MoveSpeed * percent;
                    stats.MoveSpeed += delta;
                    return delta;
                }
                case EnhancementStatType.AttackSpeed:
                {
                    float delta = stats.AttackInterval * percent;
                    stats.AttackInterval = Mathf.Max(MinAttackInterval, stats.AttackInterval - delta);
                    return delta;
                }
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// ApplyPercent가 반환한 delta를 정확히 되돌린다.
        /// </summary>
        public static void Revert(RuntimeStats stats, EnhancementStatType statType, float delta)
        {
            if (delta == 0f)
            {
                return;
            }

            switch (statType)
            {
                case EnhancementStatType.AttackPower:
                    stats.AttackPower -= delta;
                    break;
                case EnhancementStatType.MaxHealth:
                    stats.MaxHealth -= delta;
                    break;
                case EnhancementStatType.MoveSpeed:
                    stats.MoveSpeed -= delta;
                    break;
                case EnhancementStatType.AttackSpeed:
                    stats.AttackInterval += delta;
                    break;
            }
        }
    }
}
