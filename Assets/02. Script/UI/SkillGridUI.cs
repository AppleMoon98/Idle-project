using System.Collections.Generic;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 보유한 전체 스킬을 아이콘 슬롯 그리드로 상시 보여준다(SkillPanel의 빈 중하단 영역).
    /// 칸의 아이콘을 탭하면 SkillSlotBarUI에서 현재 선택된 슬롯에 그 스킬을 장착하고,
    /// 레벨 뱃지를 탭하면 레벨업 팝업(SkillDetailPopupUI)을 연다.
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
        private SkillSlotBarUI slotBar;

        [SerializeField]
        private SkillDetailPopupUI detailPopup;

        private readonly List<SkillGridCellUI> _spawnedCells = new();

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
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

                SkillGridCellUI cell = Instantiate(cellPrefab, cellContainer);
                cell.Initialize(
                    definition,
                    level,
                    onEquipTapped: () => TryEquipToSelectedSlot(definition),
                    onLevelBadgeTapped: () => detailPopup?.Open(definition));

                _spawnedCells.Add(cell);
            }
        }

        private void TryEquipToSelectedSlot(SkillSO definition)
        {
            if (slotBar == null || slotBar.SelectedSlotIndex < 0)
            {
                return;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                loadout.TryEquip(slotBar.SelectedSlotIndex, definition);
            }
        }
    }
}
