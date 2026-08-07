using System.Collections.Generic;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 보유한 전체 스킬을 아이콘 슬롯 그리드로 상시 보여준다(SkillPanel의 빈 중하단 영역).
    /// 칸을 탭하면(아이콘/레벨 뱃지 어느 쪽이든) 상세 팝업(SkillDetailPopupUI)을 연다 - 장착은
    /// 그 팝업의 장착 버튼에서 이뤄진다(SkillSlotBarUI에서 선택된 슬롯 대상).
    /// </summary>
    public sealed class SkillGridUI : MonoBehaviour
    {
        [SerializeField]
        private Transform cellContainer;

        [SerializeField]
        private SkillGridCellUI cellPrefab;

        [SerializeField]
        private SkillCatalogSO skillCatalog;

        [SerializeField]
        private SkillDetailPopupUI detailPopup;

        private readonly List<SkillGridCellUI> _spawnedCells = new();

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Subscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            GameBootstrapper.Events?.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Unsubscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            GameBootstrapper.Events?.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            Refresh();
        }

        private void OnSkillCountChanged(SkillCountChangedEvent evt)
        {
            Refresh();
        }

        private void OnSkillLoadoutChanged(SkillLoadoutChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            foreach (SkillGridCellUI cell in _spawnedCells)
            {
                Destroy(cell.gameObject);
            }

            _spawnedCells.Clear();

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillService skillService))
            {
                return;
            }

            foreach (SkillSO definition in skillCatalog.Skills)
            {
                if (definition == null)
                {
                    continue;
                }

                int level = skillService.GetLevel(definition);
                int count = skillService.GetCount(definition);

                SkillGridCellUI cell = Instantiate(cellPrefab, cellContainer);
                cell.Initialize(definition, level, count, onTapped: () => detailPopup?.Open(definition));

                _spawnedCells.Add(cell);
            }
        }
    }
}
