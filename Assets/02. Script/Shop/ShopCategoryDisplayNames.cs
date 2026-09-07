namespace Shop
{
    /// <summary>
    /// ShopCategory -> 한글 이름 매핑을 한 곳에서 관리한다(UI.StatDisplayNames,
    /// Soldier.SquadTacticDisplayNames와 같은 관례). 카테고리가 늘어나면 이 파일만 고치면 된다.
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
