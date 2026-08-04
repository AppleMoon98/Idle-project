using System;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 6개 스킬 장착 슬롯을 한 줄로 보여준다. 슬롯을 탭하면 팝업을 여는 대신 그 슬롯을
    /// "선택" 상태로 만든다(테두리 강조) — 실제 장착은 아래 SkillGridUI에서 스킬 아이콘을
    /// 탭했을 때 이 SelectedSlotIndex를 대상으로 이뤄진다.
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
        /// 현재 선택된 슬롯 인덱스. 아직 아무것도 선택하지 않았으면 -1.
        /// </summary>
        public int SelectedSlotIndex { get; private set; } = -1;

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slotIndex = i;
                slots[i].Button.onClick.AddListener(() => SelectSlot(slotIndex));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            Refresh();

            // 탭이 열리면 바로 스킬을 고를 수 있도록 첫 슬롯을 기본 선택해둔다
            // (EquippedSlotBarUI가 첫 슬롯 팝업을 자동으로 여는 것과 같은 이유).
            if (SelectedSlotIndex < 0 && slots.Length > 0)
            {
                SelectSlot(0);
            }
            else
            {
                UpdateHighlights();
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
        }

        private void OnSkillLoadoutChanged(SkillLoadoutChangedEvent evt)
        {
            Refresh();
        }

        private void SelectSlot(int slotIndex)
        {
            SelectedSlotIndex = slotIndex;
            UpdateHighlights();
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
