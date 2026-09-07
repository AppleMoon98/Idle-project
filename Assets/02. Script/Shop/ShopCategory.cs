namespace Shop
{
    /// <summary>
    /// 상점 물품을 나누는 소분류. ShopCatalogSO.GetByCategory가 이 값으로 물품을 필터링한다.
    /// 카테고리 탭 자체는 IntegratedMenuPopup의 메뉴 버튼들과 같은 관례로 씬에 고정 배치된
    /// 소수(3개)의 버튼이라, 값을 추가해도 코드는 안 바뀌지만 새 탭 버튼은 씬에 직접 만들어야
    /// 한다(ShopCategoryDisplayNames.Get으로 라벨만 코드 변경 없이 얻을 수 있다).
    /// </summary>
    public enum ShopCategory
    {
        Equipment,
        Skill,
        Special
    }
}
