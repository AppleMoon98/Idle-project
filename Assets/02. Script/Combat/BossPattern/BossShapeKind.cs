namespace Combat.BossPattern
{
    /// <summary>
    /// 보스 패턴 판정 도형의 종류. 직사각형과 부채꼴 두 원시 도형의 조합만으로 찌르기(직사각형),
    /// 반원 가르기(부채꼴 180도 2회), 부채꼴 베기, 십자/X자(직사각형 여러 개), 세로줄(직사각형)까지
    /// 전부 표현할 수 있어 그 이상의 도형 종류를 두지 않는다.
    /// </summary>
    public enum BossShapeKind
    {
        Rectangle,
        Sector
    }
}
