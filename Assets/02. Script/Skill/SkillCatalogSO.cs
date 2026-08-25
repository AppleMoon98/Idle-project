using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 게임에 존재하는 전체 스킬 목록(고정 슬롯이라 가챠 없이 전부 이 배열로 정의된다).
    /// 다른 카탈로그(StageCatalogSO 등)와 동일하게 인덱스 기반으로 세이브에서 식별한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillCatalog", menuName = "Idle Project/Skill/Skill Catalog")]
    public sealed class SkillCatalogSO : ScriptableObject
    {
        [SerializeField]
        private SkillSO[] skills;

        public SkillSO[] Skills => skills;

        public int IndexOf(SkillSO skill)
        {
            if (skills == null)
            {
                return -1;
            }

            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] == skill)
                {
                    return i;
                }
            }

            return -1;
        }

        public SkillSO GetAt(int index)
        {
            if (skills == null || index < 0 || index >= skills.Length)
            {
                return null;
            }

            return skills[index];
        }

        /// <summary>
        /// stableId가 일치하는 스킬을 반환한다. 없거나 stableId가 비어있으면 null
        /// (GitHub 이슈 #19 - EquipmentCatalogSO.FindByStableId와 동일한 이유).
        /// </summary>
        public SkillSO FindByStableId(string stableId)
        {
            if (skills == null || string.IsNullOrEmpty(stableId))
            {
                return null;
            }

            foreach (SkillSO skill in skills)
            {
                if (skill != null && skill.StableId == stableId)
                {
                    return skill;
                }
            }

            return null;
        }
    }
}
