namespace Shop
{
    /// <summary>
    /// 상점 물품을 나누는 소분류. 콘텐츠가 늘어나면 값만 추가하면 되고,
    /// UI(카테고리 탭/필터)는 이 enum을 그대로 순회하므로 코드 변경이 필요 없다.
    /// </summary>
    public enum ShopCategory
    {
        Equipment,
        Skill,
        Special
    }
}
