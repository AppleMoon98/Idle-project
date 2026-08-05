using System.Collections.Generic;
using Enhancement;

namespace UI
{
    /// <summary>
    /// EquipmentStatService.CalculatePreview가 반환하는 (StatType, Bonus) 목록에서 특정 스탯의
    /// 값을 찾는 조회 헬퍼. EquipmentDetailPopupUI(비교 대상 옵션 조회)와
    /// EquipmentEnhancementPopupUI(다음 레벨 옵션 조회)가 각자 들고 있던 동일한 선형 탐색을 공유한다.
    /// </summary>
    public static class StatOptionLookup
    {
        /// <summary>
        /// options에서 statType과 일치하는 항목의 Bonus를 반환한다. 없으면 0.
        /// </summary>
        public static float FindBonus(IReadOnlyList<(EnhancementStatType StatType, float Bonus)> options, EnhancementStatType statType)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].StatType == statType)
                {
                    return options[i].Bonus;
                }
            }

            return 0f;
        }
    }
}
