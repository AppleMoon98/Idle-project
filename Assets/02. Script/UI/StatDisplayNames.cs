using Enhancement;

namespace UI
{
    /// <summary>
    /// EnhancementStatType을 화면에 보여줄 한국어 이름으로 변환하는 단일 매핑 지점.
    /// StatRowUI(강화 패널)와 EquipmentDetailPopupUI(장비 옵션 비교)가 이 헬퍼를 공유해서,
    /// 새 능력치(공격속도 등)가 추가될 때 이름 표시를 손댈 곳을 한 곳으로 줄인다.
    /// </summary>
    public static class StatDisplayNames
    {
        public static string Get(EnhancementStatType statType)
        {
            return statType switch
            {
                EnhancementStatType.AttackPower => "공격력",
                EnhancementStatType.MaxHealth => "최대 체력",
                EnhancementStatType.AttackSpeed => "공격속도",
                EnhancementStatType.MoveSpeed => "이동속도",
                EnhancementStatType.CriticalChance => "크리티컬 확률",
                EnhancementStatType.CriticalDamage => "크리티컬 피해량",
                _ => statType.ToString()
            };
        }

        /// <summary>
        /// 능력치 값을 표시용 문자열로 포맷한다. 공격속도/이동속도/크리티컬 확률/피해량은 원본 값
        /// 대비 비율(0.01 = 1%)이라 %로 표기하고, 나머지(공격력/체력 등 절대값 스탯)는 숫자 그대로 보여준다.
        /// </summary>
        public static string FormatValue(EnhancementStatType statType, float value)
        {
            return statType switch
            {
                EnhancementStatType.AttackSpeed => $"{value * 100f:0.##}%",
                EnhancementStatType.MoveSpeed => $"{value * 100f:0.##}%",
                EnhancementStatType.CriticalChance => $"{value * 100f:0.##}%",
                EnhancementStatType.CriticalDamage => $"{value * 100f:0.##}%",
                _ => value.ToString("0.##")
            };
        }
    }
}
