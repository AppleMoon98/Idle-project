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
    /// 누르면 그 장비를 장착한다.
    /// </summary>
    public sealed class EquipmentSlotPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private GameObject rowPrefab;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        private EquipmentType _openSlot;
        private bool _isOpen;
        private readonly List<GameObject> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
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

        private void Refresh()
        {
            foreach (GameObject row in _spawnedRows)
            {
                Destroy(row);
            }

            _spawnedRows.Clear();

            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out InventoryService inventory)
                || !GameBootstrapper.Services.TryGet(out EquippedGearService equippedGear))
            {
                return;
            }

            IEnumerable<OwnedEquipment> matching = inventory.Items
                .Where(owned => owned.Definition.EquipmentType == _openSlot)
                .OrderBy(owned => gradeCatalog.IndexOf(owned.Definition.Grade));

            foreach (OwnedEquipment owned in matching)
            {
                GameObject row = Instantiate(rowPrefab, rowContainer);
                row.GetComponentInChildren<Text>().text =
                    $"{owned.Definition.ItemName} x{owned.Count} (강화 {owned.EnhancementLevel})";

                row.GetComponentInChildren<Button>().onClick.AddListener(() =>
                {
                    equippedGear.Equip(owned);
                    Close();
                });

                _spawnedRows.Add(row);
            }
        }
    }
}
