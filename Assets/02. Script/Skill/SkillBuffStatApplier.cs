using Character;
using Enhancement;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// "현재 스탯 값 대비 %만큼 올리거나(양수) 내리고(음수), 나중에 정확히 그만큼만 되돌린다"는
    /// 스킬 버프/디버프 공통 로직. Character.PossessionStatApplier(장비 보유 효과, 원본 스탯 대비 %)와
    /// 달리 "현재 값" 기준이다 - 버프/디버프는 서로 다른 스킬이 겹쳐 걸려도 각자 시전 시점의 현재
    /// 값을 기준으로 계산하고 그 배율만 정확히 되돌리면 되므로, 원본(BaseStats) 참조가 필요 없다.
    /// 공격속도만 예외적으로 "값이 작을수록 빠르다"는 AttackInterval의 반대 방향 의미라 부호를
    /// 뒤집어 적용한다(PossessionStatApplier의 AttackSpeed 처리와 동일한 관례).
    ///
    /// **곱연산(배율)으로 적용/복원하는 이유:** 서로 다른 두 스킬이 같은 스탯을 동시에 버프하면
    /// (예: 공속 +10% 스킬과 +15% 스킬을 함께 사용) 두 번째는 첫 번째가 이미 적용된 "현재 값"
    /// 위에 걸리므로 자연히 곱연산으로 누적된다(최종 = base*(1-0.10)*(1-0.15), 단순 합산인 -25%가
    /// 아니다). 되돌릴 때도 절대값 델타를 빼는 대신 배율로 나눠야 한다 - 절대값 델타로 되돌리면,
    /// 그 사이 다른 스킬이 같은 필드를 또 곱연산으로 건드렸을 때 정확히 상쇄되지 않고 오차가
    /// 남는다(배율로 나누는 연산은 다른 곱연산 레이어가 몇 겹 끼어 있어도 교환·결합 법칙에 따라
    /// 항상 정확히 자기 몫만 제거하지만, 뺄셈은 그렇지 않다).
    /// </summary>
    public static class SkillBuffStatApplier
    {
        private const float MinAttackInterval = 0.05f;

        /// <summary>
        /// stats의 statType 값에 percent만큼(현재 값 대비 비율, 예: 0.1 = +10%, -0.2 = -20%)을
        /// 곱연산으로 적용하고, 실제로 적용된 percent를 그대로 반환한다 - 호출자는 이 값을
        /// 저장해뒀다가 Revert에 그대로 넘겨야 한다(무슨 절대값인지가 아니라 "몇 %를 곱했는지"를
        /// 기억해두는 것). 지원하지 않는 스탯 타입이면 아무 일도 하지 않고 0을 반환한다.
        /// </summary>
        public static float ApplyPercent(RuntimeStats stats, EnhancementStatType statType, float percent)
        {
            switch (statType)
            {
                case EnhancementStatType.AttackPower:
                    stats.AttackPower *= 1f + percent;
                    return percent;
                case EnhancementStatType.MaxHealth:
                    stats.MaxHealth *= 1f + percent;
                    return percent;
                case EnhancementStatType.MoveSpeed:
                    stats.MoveSpeed *= 1f + percent;
                    return percent;
                case EnhancementStatType.AttackSpeed:
                    stats.AttackInterval = Mathf.Max(MinAttackInterval, stats.AttackInterval * (1f - percent));
                    return percent;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// ApplyPercent가 반환한 percent를 배율로 나눠 정확히 되돌린다 - 그 사이 다른 스킬이
        /// 같은 스탯에 곱연산 버프를 더 걸었어도(교환·결합 법칙 덕분에) 이 버프의 몫만 정확히
        /// 제거된다.
        /// </summary>
        public static void Revert(RuntimeStats stats, EnhancementStatType statType, float percent)
        {
            if (percent == 0f)
            {
                return;
            }

            switch (statType)
            {
                case EnhancementStatType.AttackPower:
                    stats.AttackPower /= 1f + percent;
                    break;
                case EnhancementStatType.MaxHealth:
                    stats.MaxHealth /= 1f + percent;
                    break;
                case EnhancementStatType.MoveSpeed:
                    stats.MoveSpeed /= 1f + percent;
                    break;
                case EnhancementStatType.AttackSpeed:
                    stats.AttackInterval /= 1f - percent;
                    break;
            }
        }
    }
}
