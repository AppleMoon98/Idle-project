using System;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 슬롯을 아이콘 바 형태로 나열한다(Equipment.EquippedSlotBarUI와 동일한 패턴). 각 슬롯은
    /// 지금 레벨을 보여주고, 누르면 그 스킬의 상세/레벨업 팝업을 연다.
    /// </summary>
    public sealed class SkillSlotBarUI : MonoBehaviour
    {
        [Serializable]
        private struct SlotUI
        {
            public SkillSO Skill;
            public Button Button;
            public Image Icon;
            public Text LevelText;
        }

        [SerializeField]
        private SlotUI[] slots;

        [SerializeField]
        private SkillDetailPopupUI popup;

        private void Awake()
        {
            foreach (SlotUI slot in slots)
            {
                if (slot.Skill == null)
                {
                    continue;
                }

                slot.Icon.sprite = slot.Skill.Icon;
                slot.Icon.color = slot.Skill.IconTint;

                SkillSO skill = slot.Skill;
                slot.Button.onClick.AddListener(() => popup.Open(skill));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            popup?.Close();
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillService service))
            {
                return;
            }

            foreach (SlotUI slot in slots)
            {
                if (slot.Skill == null)
                {
                    continue;
                }

                slot.LevelText.text = $"Lv.{service.GetLevel(slot.Skill)}";
            }
        }
    }
}
