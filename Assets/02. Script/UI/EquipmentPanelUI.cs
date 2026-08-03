using System.Collections.Generic;
using System.Linq;
using Core;
using Equipment;
using Inventory;
using Inventory.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 보유 장비 전체를 슬롯→등급 순으로 카드 리스트로 보여준다. 슬롯 팝업(EquipmentSlotPopupUI)과
    /// 동일한 EquipmentRowUI를 재사용해 등급색 배경/장착 표시/합성·강화 버튼을 그대로 제공한다.
    /// </summary>
    public sealed class EquipmentPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private EquipmentRowUI rowPrefab;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        [SerializeField]
        private Color cardBaseColor = new Color(0.13f, 0.10f, 0.08f, 0.92f);

        [SerializeField]
        [Range(0f, 1f)]
        private float gradeTintBlend = 0.35f;

        private readonly List<EquipmentRowUI> _spawnedRows = new();

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            Refresh();
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            foreach (EquipmentRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out InventoryService inventory)
                || !GameBootstrapper.Services.TryGet(out EquippedGearService equippedGear))
            {
                return;
            }

            IEnumerable<OwnedEquipment> sorted = inventory.Items
                .OrderBy(owned => owned.Definition.EquipmentType)
                .ThenBy(owned => gradeCatalog.IndexOf(owned.Definition.Grade));

            foreach (OwnedEquipment owned in sorted)
            {
                bool isEquipped = equippedGear.GetEquipped(owned.Definition.EquipmentType) == owned;
                Color backgroundColor = EquipmentRowUI.ComputeGradeBackground(cardBaseColor, owned.Definition.Grade, gradeTintBlend);

                EquipmentRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(owned, isEquipped, backgroundColor);

                _spawnedRows.Add(row);
            }
        }
    }
}
