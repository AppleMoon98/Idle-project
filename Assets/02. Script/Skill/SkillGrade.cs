namespace Skill
{
    /// <summary>
    /// 스킬의 등급. 장비 등급(EquipmentGradeCatalogSO)처럼 세부 단계를 여러 개 두는 사다리가
    /// 아니라, 고정된 6종 하나로 끝나는 분류라 콘텐츠 확장 가능성이 필요한 장비와 달리
    /// 일반 enum으로 충분하다.
    /// </summary>
    public enum SkillGrade
    {
        Common,
        Uncommon,
        Rare,
        SuperRare,
        Epic,
        Legendary
    }
}
