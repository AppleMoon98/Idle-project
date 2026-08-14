using System;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 6개 스킬 장착 슬롯을 한 줄로 보여준다. 슬롯 탭 상호작용은 UI.SquadDeploymentSlotGridUI와
    /// 같은 "선택 → 같은 슬롯 재탭=해제 / 다른 슬롯 탭=자리교환" 모델을 쓴다: 빈/채워진 슬롯
    /// 아무거나 탭하면 선택(테두리)되고, 그 상태에서 같은 슬롯을 다시 탭하면 장착 해제, 다른
    /// 슬롯을 탭하면 두 슬롯의 내용이 서로 바뀐다(SkillLoadoutService.Swap).
    ///
    /// "어떤 스킬을 장착할지"는 여전히 아래 SkillGridUI → SkillDetailPopupUI의 장착 버튼에서
    /// 정해진다 - 슬롯이 미리 선택돼 있으면 그 슬롯에 바로 장착하고, 선택된 슬롯이 없으면 이
    /// 컴포넌트에 "대기 중인 스킬"로 넘겨(RequestEquipTarget) selectSlotPromptText를 띄운 채
    /// 다음 슬롯 탭을 기다렸다가 그 슬롯에 장착한다.
    /// </summary>
    public sealed class SkillSlotBarUI : MonoBehaviour
    {
        [Serializable]
        private struct SlotUI
        {
            public Button Button;
            public Image Icon;
            public Text LevelText;
            public GameObject SelectedHighlight;
        }

        [SerializeField]
        private SlotUI[] slots;

        [SerializeField]
        private Color emptySlotIconColor = new(0.3f, 0.3f, 0.3f, 1f);

        /// <summary>
        /// SubTabBar와 이 슬롯 바 사이에 배치된 안내 텍스트("스킬을 장착할 슬롯을 선택해주세요.") -
        /// 다른 오브젝트 위치는 그대로 두고 이 텍스트만 켜고 끈다.
        /// </summary>
        [SerializeField]
        private GameObject selectSlotPromptText;

        private SkillSO _pendingEquipSkill;

        /// <summary>
        /// 현재 선택된 슬롯 인덱스. 아직 아무것도 선택하지 않았으면 -1.
        /// </summary>
        public int SelectedSlotIndex { get; private set; } = -1;

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slotIndex = i;
                slots[i].Button.onClick.AddListener(() => OnSlotTapped(slotIndex));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);

            SelectedSlotIndex = -1;
            _pendingEquipSkill = null;
            SetPromptVisible(false);

            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
        }

        private void OnSkillLoadoutChanged(SkillLoadoutChangedEvent evt)
        {
            Refresh();
        }

        /// <summary>
        /// SkillDetailPopupUI의 장착 버튼이 선택된 슬롯 없이 눌렸을 때 호출한다. 이 스킬을
        /// "대기" 상태로 넘겨두고 안내 텍스트를 띄운다 - 이후 어떤 슬롯이든 탭되는 순간
        /// (OnSlotTapped의 "선택된 슬롯 없음" 분기) 그 슬롯에 장착되고 안내는 사라진다.
        /// </summary>
        public void RequestEquipTarget(SkillSO definition)
        {
            _pendingEquipSkill = definition;
            SetPromptVisible(true);
        }

        /// <summary>
        /// 선택 상태를 해제한다. SkillDetailPopupUI가 이미 선택된 슬롯에 바로 장착을 완료했을 때
        /// 호출한다 - 장착 후에도 선택이 계속 남아있으면, 다음에 다른 스킬을 장착하려고 다른
        /// 슬롯을 탭했을 때 "새로 선택"이 아니라 "방금 장착한 슬롯과 자리 교환"으로 잘못
        /// 해석되어 방금 장착한 스킬이 엉뚱한 슬롯으로 옮겨간 것처럼 보이는 문제가 있었다.
        /// </summary>
        public void ClearSelection()
        {
            SelectedSlotIndex = -1;
            UpdateHighlights();
        }

        private void OnSlotTapped(int slotIndex)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                return;
            }

            if (SelectedSlotIndex == slotIndex)
            {
                SelectedSlotIndex = -1;
                loadout.Unequip(slotIndex);
                UpdateHighlights();
                return;
            }

            if (SelectedSlotIndex != -1)
            {
                int sourceSlotIndex = SelectedSlotIndex;
                SelectedSlotIndex = -1;
                loadout.Swap(sourceSlotIndex, slotIndex);
                UpdateHighlights();
                return;
            }

            // 대기 중인 스킬이 있으면 이 탭으로 바로 장착하고 끝낸다 - 선택 상태로 남겨두지
            // 않는다(장착 완료 후에도 선택이 남아있으면 다음 슬롯 탭이 "새 선택"이 아니라
            // "이 슬롯과 자리 교환"으로 잘못 해석되는 문제가 있었다, ClearSelection 참고).
            if (_pendingEquipSkill != null)
            {
                loadout.TryEquip(slotIndex, _pendingEquipSkill);
                _pendingEquipSkill = null;
                SetPromptVisible(false);
                SelectedSlotIndex = -1;
                UpdateHighlights();
                return;
            }

            SelectedSlotIndex = slotIndex;
            UpdateHighlights();
        }

        private void SetPromptVisible(bool visible)
        {
            if (selectSlotPromptText != null)
            {
                selectSlotPromptText.SetActive(visible);
            }
        }

        private void UpdateHighlights()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].SelectedHighlight != null)
                {
                    slots[i].SelectedHighlight.SetActive(i == SelectedSlotIndex);
                }
            }
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout)
                || !GameBootstrapper.Services.TryGet(out SkillService skillService))
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                SkillSO definition = loadout.GetEquipped(i);

                if (definition == null)
                {
                    slots[i].Icon.sprite = null;
                    slots[i].Icon.color = emptySlotIconColor;
                    slots[i].LevelText.text = "";
                    continue;
                }

                slots[i].Icon.sprite = definition.Icon;
                slots[i].Icon.color = definition.IconTint;
                slots[i].LevelText.text = $"Lv.{skillService.GetLevel(definition)}";
            }

            UpdateHighlights();
        }
    }
}
