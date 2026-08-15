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
    public sealed class SkillGridUI : MonoBehaviour, ITickable
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
        private SkillSO _pendingHighlightSkill;
        private bool _isRefreshPending;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Subscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            GameBootstrapper.Events?.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            TickerRegistration.Register(this);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Unsubscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            GameBootstrapper.Events?.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// "전체 레벨업"(SkillLevelUpAllButtonUI) 한 번의 클릭이 SkillLeveledUpEvent를 스킬 수만큼
        /// 연달아 발행할 수 있는데, 그때마다 Refresh()로 그리드 전체를 Destroy+Instantiate로 다시
        /// 그리면 그 프레임에 몰려서 눈에 띄는 멈춤이 생긴다 — EquipmentSlotPopupUI가 전체
        /// 합성/강화에서 이미 겪은 것과 동일한 문제(section CL)라 같은 방식으로 예방한다. 이벤트가
        /// 오면 더티 플래그만 세우고, 실제 Refresh()는 프레임당 최대 한 번만 수행한다.
        /// </summary>
        void ITickable.Tick(float deltaTime)
        {
            if (!_isRefreshPending)
            {
                return;
            }

            _isRefreshPending = false;
            Refresh();
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            _isRefreshPending = true;
        }

        private void OnSkillCountChanged(SkillCountChangedEvent evt)
        {
            _isRefreshPending = true;
        }

        private void OnSkillLoadoutChanged(SkillLoadoutChangedEvent evt)
        {
            _isRefreshPending = true;
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
                cell.SetSelected(definition == _pendingHighlightSkill);

                _spawnedCells.Add(cell);
            }
        }

        /// <summary>
        /// skill과 일치하는 칸에만 테두리를 켠다(SkillSlotBarUI가 장착 대기 중인 스킬을 알려줄 때
        /// 호출) - null이면 전부 끈다. Refresh()가 칸을 매번 Destroy+Instantiate로 다시 그리므로
        /// (레벨업/보유개수/장착 변경 시), 그 사이에도 대기 상태가 이어질 수 있어 값을 기억해뒀다가
        /// Refresh() 안에서도 재적용한다.
        /// </summary>
        public void SetPendingSkillHighlight(SkillSO skill)
        {
            _pendingHighlightSkill = skill;

            foreach (SkillGridCellUI cell in _spawnedCells)
            {
                cell.SetSelected(cell.Definition == skill);
            }
        }
    }
}
