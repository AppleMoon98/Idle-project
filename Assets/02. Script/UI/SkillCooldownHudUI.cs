using System;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// BottomMenu 위에 상시 떠 있는, 장착된 6개 스킬의 쿨다운을 보여주는 HUD. 아이콘은 장착이
    /// 바뀔 때만(SkillLoadoutChangedEvent) 갱신하고, 쿨다운 진행률은 매 틱 해당 SkillSlot의
    /// CooldownProgress01을 읽어 원형 마스크로 표시한다(HealthBarUI와 같은 ITickable 방식 —
    /// 판정은 SkillSlot이 갖고 이 컴포넌트는 그리기만 한다). 슬롯을 탭하면 자동 발동 켜짐/꺼짐을
    /// 토글하고(Border로 표시, 기본값 켜짐), 실제 판정은 SkillLoadoutService.IsEnabled를 SkillSlot이
    /// 직접 확인해서 걸러낸다 - 이 컴포넌트는 그 상태를 보여주고 토글만 요청할 뿐이다.
    /// </summary>
    public sealed class SkillCooldownHudUI : MonoBehaviour, ITickable
    {
        [Serializable]
        private struct SlotUI
        {
            public Button Button;
            public Image Icon;
            public Image CooldownOverlay;
            public GameObject Border;
        }

        [SerializeField]
        private SlotUI[] slots;

        /// <summary>
        /// 실제 시전을 담당하는 Player의 SkillSlot 6개. slots 배열과 인덱스가 대응한다.
        /// </summary>
        [SerializeField]
        private SkillSlot[] casterSlots;

        [SerializeField]
        private Color emptySlotIconColor = new(0.3f, 0.3f, 0.3f, 1f);

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slotIndex = i;
                slots[i].Button.onClick.AddListener(() => ToggleSlot(slotIndex));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            GameBootstrapper.Events?.Subscribe<SkillSlotEnabledChangedEvent>(OnSkillSlotEnabledChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            RefreshIcons();
            RefreshAllBorders();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            GameBootstrapper.Events?.Unsubscribe<SkillSlotEnabledChangedEvent>(OnSkillSlotEnabledChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnSkillLoadoutChanged(SkillLoadoutChangedEvent evt)
        {
            RefreshIcons();
        }

        private void OnSkillSlotEnabledChanged(SkillSlotEnabledChangedEvent evt)
        {
            if (evt.SlotIndex >= 0 && evt.SlotIndex < slots.Length && slots[evt.SlotIndex].Border != null)
            {
                slots[evt.SlotIndex].Border.SetActive(evt.IsEnabled);
            }
        }

        private void ToggleSlot(int slotIndex)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                loadout.ToggleEnabled(slotIndex);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            for (int i = 0; i < slots.Length && i < casterSlots.Length; i++)
            {
                if (slots[i].CooldownOverlay == null || casterSlots[i] == null)
                {
                    continue;
                }

                // 쿨다운이 막 시작됐을 땐 오버레이가 아이콘을 완전히 덮고(1), 다 찰수록 걷힌다(0).
                slots[i].CooldownOverlay.fillAmount = 1f - casterSlots[i].CooldownProgress01;
            }
        }

        private void RefreshIcons()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
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
                    continue;
                }

                slots[i].Icon.sprite = definition.Icon;
                slots[i].Icon.color = definition.IconTint;
            }
        }

        private void RefreshAllBorders()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Border != null)
                {
                    slots[i].Border.SetActive(loadout.IsEnabled(i));
                }
            }
        }
    }
}
