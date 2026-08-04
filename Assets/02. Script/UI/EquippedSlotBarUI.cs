using System;
using Core;
using Equipment;
using Inventory;
using Inventory.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 상단에 슬롯(무기/장갑/갑옷/투구/신발) 순서대로 버튼을 나열한다. 각 버튼은 지금
    /// 장착 중인 장비 이름을 보여주고, 누르면 그 슬롯의 보유 장비 목록 팝업을 연다.
    /// </summary>
    public sealed class EquippedSlotBarUI : MonoBehaviour
    {
        [Serializable]
        private struct SlotUI
        {
            public EquipmentType Type;
            public Button Button;
            public Text Label;
            public Image Frame;
        }

        [SerializeField]
        private SlotUI[] slots;

        [SerializeField]
        private EquipmentSlotPopupUI popup;

        [SerializeField]
        private Color emptySlotFrameColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        [SerializeField]
        private GameObject skillCooldownHud;

        private void Awake()
        {
            foreach (SlotUI slot in slots)
            {
                EquipmentType type = slot.Type;
                slot.Button.onClick.AddListener(() => popup.Open(type));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            Refresh();

            // 장비 탭을 열면 첫 번째 슬롯(무기)의 팝업을 자동으로 띄워, 슬롯바가 뜨자마자
            // 바로 장비 목록을 볼 수 있게 한다.
            if (slots.Length > 0)
            {
                popup.Open(slots[0].Type);
            }

            // 스킬 쿨다운 HUD(BottomMenu 위 상시 표시)가 장비 슬롯바와 화면 위치가 겹치므로,
            // 장비 탭이 열려있는 동안은 잠깐 숨겨둔다.
            skillCooldownHud?.SetActive(false);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);

            // 슬롯바가 꺼질 때 팝업이 따로 떠 있는 채로 남지 않도록 같이 닫는다
            // (팝업은 슬롯바의 자식이 아니라 Canvas의 별도 형제 오브젝트라 자동으로 꺼지지 않는다).
            popup.Close();

            skillCooldownHud?.SetActive(true);
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EquippedGearService equippedGear))
            {
                return;
            }

            foreach (SlotUI slot in slots)
            {
                OwnedEquipment owned = equippedGear.GetEquipped(slot.Type);
                slot.Label.text = owned != null ? owned.Definition.ItemName : "-";

                if (slot.Frame != null)
                {
                    bool hasGrade = owned != null && owned.Definition.Grade != null;
                    slot.Frame.color = hasGrade ? owned.Definition.Grade.TintColor : emptySlotFrameColor;
                }
            }
        }
    }
}
