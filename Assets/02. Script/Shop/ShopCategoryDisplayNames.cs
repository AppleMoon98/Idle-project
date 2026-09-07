namespace Shop
{
    /// <summary>
    /// ShopCategory -> 한글 이름 매핑을 한 곳에서 관리한다(UI.StatDisplayNames와 같은 관례 -
    /// UI.ShopCategoryTabButtonUI가 탭 버튼 라벨을 이 값으로 채운다). 표시 이름이 바뀌면
    /// 이 파일만 고치면 되고, 씬의 버튼을 다시 저장할 필요가 없다.
    /// </summary>
    public static class ShopCategoryDisplayNames
    {
        public static string Get(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.Equipment:
                    return "장비";
                case ShopCategory.Skill:
                    return "스킬";
                case ShopCategory.Special:
                    return "특별";
                default:
                    return category.ToString();
            }
        }
    }
}
