using Skill;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// SkillCatalogSO에 등록된 스킬 수만큼 SkillSlotRowUI를 생성해 배치한다. 스킬은 고정 슬롯이라
    /// 카탈로그에 에셋을 추가/제거하는 것만으로 이 패널의 행이 그대로 늘고 준다(StatPanelUI와 동일한 패턴).
    /// </summary>
    public sealed class SkillPanelUI : MonoBehaviour
    {
        [SerializeField]
        private SkillCatalogSO catalog;

        [SerializeField]
        private SkillSlotRowUI rowPrefab;

        [SerializeField]
        private Transform rowParent;

        private void Awake()
        {
            if (catalog == null || catalog.Skills == null)
            {
                return;
            }

            foreach (SkillSO skill in catalog.Skills)
            {
                SkillSlotRowUI row = Instantiate(rowPrefab, rowParent);
                row.Initialize(skill);
            }
        }
    }
}
