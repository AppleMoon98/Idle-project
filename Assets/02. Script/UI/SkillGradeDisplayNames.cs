using Skill;

namespace UI
{
    /// <summary>
    /// SkillGrade를 화면에 보여줄 한국어 이름으로 변환하는 단일 매핑 지점. StatDisplayNames와
    /// 동일한 목적(새 등급이 추가되면 손댈 곳을 한 곳으로 줄인다).
    /// </summary>
    public static class SkillGradeDisplayNames
    {
        public static string Get(SkillGrade grade)
        {
            return grade switch
            {
                SkillGrade.Common => "커먼",
                SkillGrade.Uncommon => "언커먼",
                SkillGrade.Rare => "레어",
                SkillGrade.SuperRare => "슈퍼레어",
                SkillGrade.Epic => "에픽",
                SkillGrade.Legendary => "레전더리",
                _ => grade.ToString()
            };
        }
    }
}
