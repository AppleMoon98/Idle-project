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
                _ => statType.ToString()
            };
        }
    }
}
