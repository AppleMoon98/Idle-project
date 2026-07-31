using System.Collections.Generic;
using System.Linq;
using Core;
using Equipment;
using Inventory;
using Inventory.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 슬롯 하나의 보유 장비 목록을 등급순으로 나열하는 팝업. 같은 라인은 하나의 행으로
    /// 묶여 "xN"으로 표시되고(EquipmentPanelUI와 동일한 OwnedEquipment 스택 방식), 행을
    /// 누르면 장착, 각 행의 합성/강화 버튼으로 바로 성장시킬 수 있다.
    /// </summary>
    public sealed class EquipmentSlotPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private EquipmentRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        [SerializeField]
        private Color cardBaseColor = new Color(0.13f, 0.10f, 0.08f, 0.92f);

        [SerializeField]
        [Range(0f, 1f)]
        private float gradeTintBlend = 0.35f;

        private EquipmentType _openSlot;
        private bool _isOpen;
        private readonly List<EquipmentRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
        }

        /// <summary>
        /// slot의 보유 장비 목록을 채워 팝업을 연다.
        /// </summary>
        public void Open(EquipmentType slot)
        {
            _openSlot = slot;
            _isOpen = true;
            popupRoot.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            if (_isOpen)
            {
                Refresh();
            }
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            if (_isOpen)
            {
                Refresh();
            }
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

            OwnedEquipment currentlyEquipped = equippedGear.GetEquipped(_openSlot);

            IEnumerable<OwnedEquipment> matching = inventory.Items
                .Where(owned => owned.Definition.EquipmentType == _openSlot)
                .OrderBy(owned => gradeCatalog.IndexOf(owned.Definition.Grade));

            foreach (OwnedEquipment owned in matching)
            {
                EquipmentRowUI row = Instantiate(rowPrefab, rowContainer);
                Color backgroundColor = EquipmentRowUI.ComputeGradeBackground(cardBaseColor, owned.Definition.Grade, gradeTintBlend);
                row.Initialize(owned, owned == currentlyEquipped, backgroundColor, Close);

                _spawnedRows.Add(row);
            }
        }
    }
}
