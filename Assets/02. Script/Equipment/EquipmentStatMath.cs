using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 슬롯 계수(EquipmentStatConfigSO.SlotStatEntry) + 등급 인덱스 + 강화 레벨로 실제 능력치
    /// 보너스 값을 계산하는 순수 함수. EquipmentStatService(장착 합계 계산)와 UI(장착 여부와
    /// 무관한 아이템 미리보기)가 같은 공식을 공유하도록 뽑아낸 헬퍼다.
    /// </summary>
    public static class EquipmentStatMath
    {
        /// <summary>
        /// entry의 슬롯 계수를 등급 인덱스·강화 레벨에 적용해 실제 보너스 값을 계산한다.
        /// gradeIndex는 음수면 0으로 취급한다(등급 카탈로그에 없는 등급 등 방어적 처리).
        /// </summary>
        public static float CalculateBonus(
            EquipmentStatConfigSO.SlotStatEntry entry,
            int gradeIndex,
            int enhancementLevel,
            float statBonusPerLevel)
        {
            int clampedGradeIndex = Mathf.Max(gradeIndex, 0);
            float baseline = entry.BaseValue + entry.PerGradeIndex * clampedGradeIndex;
            baseline += ResolveGradeThresholdBonus(entry.GradeThresholdBonuses, clampedGradeIndex);

            return baseline * (1f + statBonusPerLevel * enhancementLevel);
        }

        /// <summary>
        /// 조건을 만족하는 구간 중 GradeIndexThreshold가 가장 큰 것의 BonusAmount를 고른다(순서
        /// 무관 - 배열이 오름차순으로 정렬돼 있지 않아도 안전). 만족하는 구간이 없으면 0.
        /// </summary>
        private static float ResolveGradeThresholdBonus(EquipmentStatConfigSO.GradeThresholdBonusTier[] tiers, int gradeIndex)
        {
            if (tiers == null)
            {
                return 0f;
            }

            float bonus = 0f;
            int bestThreshold = int.MinValue;

            for (int i = 0; i < tiers.Length; i++)
            {
                EquipmentStatConfigSO.GradeThresholdBonusTier tier = tiers[i];

                if (gradeIndex >= tier.GradeIndexThreshold && tier.GradeIndexThreshold > bestThreshold)
                {
                    bestThreshold = tier.GradeIndexThreshold;
                    bonus = tier.BonusAmount;
                }
            }

            return bonus;
        }
    }
}
