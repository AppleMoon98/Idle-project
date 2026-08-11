namespace Gacha
{
    /// <summary>
    /// 가챠 테이블(GachaTableSO/SkillGachaTableSO) 한 티어가 소모하는 재화 종류. 기본값은
    /// Ticket이라 기존에 만들어둔 테이블 에셋은 아무 변경 없이 그대로 티켓/주문서를 소모한다 —
    /// 이 필드가 새로 생겼다고 기존 콘텐츠가 깨지지 않도록 하기 위한 "0(첫 값)이 기존 동작"
    /// 관례(EquipmentGradeSO.RequiredStage == null과 같은 성격).
    /// </summary>
    public enum GachaCurrencyType
    {
        Ticket,
        Gold
    }
}
