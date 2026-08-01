using System.Collections.Generic;
using System.Linq;
using Core;
using SoldierEquipment;
using SoldierEquipment.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 특정 병사 유닛(InstanceId)의 무기+방어구 슬롯을 한 팝업에서 같이 보여준다. 플레이어
    /// Equipment는 슬롯마다 팝업이 따로 있지만(EquipmentSlotPopupUI), 병사는 슬롯이 2개뿐이라
    /// 카탈로그 전체를 슬롯순으로 한 목록에 나열하는 편이 더 단순하다. 보유하지 않은 아이템도
    /// 개수 0으로 표시된다(획득 경로가 아직 없어 "보유한 것만" 표시하면 목록이 항상 비어있음).
    /// </summary>
    public sealed class SoldierEquipmentPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierEquipmentRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private SoldierEquipmentCatalogSO catalog;

        private int _openInstanceId;
        private bool _isOpen;
        private readonly List<SoldierEquipmentRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierEquipmentInventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Subscribe<SoldierEquipmentEquippedEvent>(OnEquipped);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierEquipmentInventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Unsubscribe<SoldierEquipmentEquippedEvent>(OnEquipped);
        }

        /// <summary>
        /// instanceId 유닛의 장비 목록을 채워 팝업을 연다.
        /// </summary>
        public void Open(int instanceId)
        {
            _openInstanceId = instanceId;
            _isOpen = true;
            popupRoot.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// 팝업을 닫는다. SoldierRosterPanelUI가 비활성화될 때 같이 닫을 수 있도록 공개해둔다.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
        }

        private void OnInventoryChanged(SoldierEquipmentInventoryChangedEvent evt)
        {
            if (_isOpen)
            {
                Refresh();
            }
        }

        private void OnEquipped(SoldierEquipmentEquippedEvent evt)
        {
            if (_isOpen && evt.InstanceId == _openInstanceId)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            foreach (SoldierEquipmentRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SoldierEquipmentInventoryService inventory)
                || !GameBootstrapper.Services.TryGet(out SoldierEquippedGearService equippedGear)
                || catalog.Items == null)
            {
                return;
            }

            IEnumerable<SoldierEquipmentSO> sorted = catalog.Items
                .Where(item => item != null)
                .OrderBy(item => item.SlotType)
                .ThenBy(item => item.ItemName);

            foreach (SoldierEquipmentSO item in sorted)
            {
                int ownedCount = inventory.TryGet(item, out OwnedSoldierEquipment owned) ? owned.Count : 0;
                bool isEquipped = equippedGear.GetEquipped(_openInstanceId, item.SlotType)?.Definition == item;

                SoldierEquipmentRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(item, ownedCount, isEquipped, _openInstanceId);

                _spawnedRows.Add(row);
            }
        }
    }
}
