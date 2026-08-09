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
    public sealed class EquipmentSlotPopupUI : MonoBehaviour, ITickable
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
        private Button enhanceAllButton;

        [SerializeField]
        private Button fuseAllButton;

        [SerializeField]
        private EquipmentDetailPopupUI detailPopup;

        [SerializeField]
        private EquipmentEnhancementPopupUI enhancementPopup;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        [SerializeField]
        private Color cardBaseColor = new Color(0.13f, 0.10f, 0.08f, 0.92f);

        [SerializeField]
        [Range(0f, 1f)]
        private float gradeTintBlend = 0.35f;

        private EquipmentType _openSlot;
        private bool _isOpen;
        private bool _isRefreshPending;
        private readonly List<EquipmentRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
            enhanceAllButton.onClick.AddListener(EnhanceAll);
            fuseAllButton.onClick.AddListener(FuseAll);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            GameBootstrapper.Events?.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// 전체 합성/강화(EquipmentFusionService.TryFuseAll 등) 한 번의 클릭이 InventoryChangedEvent를
        /// 수십~수백 번 연달아 발행할 수 있는데, 그때마다 Refresh()로 목록 전체를 Destroy+Instantiate로
        /// 다시 그리면 그 프레임에 몰려서 눈에 띄는 멈춤이 생긴다(실사용 중 발견 - 대량 뽑기 후 전체
        /// 합성 시 재현). 이벤트가 오면 더티 플래그만 세우고, 실제 Refresh()는 프레임당 최대 한 번만
        /// 수행한다.
        /// </summary>
        void ITickable.Tick(float deltaTime)
        {
            if (!_isRefreshPending)
            {
                return;
            }

            _isRefreshPending = false;

            if (_isOpen)
            {
                Refresh();
            }
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

        /// <summary>
        /// 팝업을 닫는다. EquippedSlotBarUI가 자신이 비활성화될 때(장비 탭을 닫을 때) 이 팝업도
        /// 같이 닫기 위해 외부에서 호출할 수 있어야 한다.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
            detailPopup?.Close();
            enhancementPopup?.Close();
        }

        // 목록 갱신은 별도로 호출할 필요가 없다 - TryEnhance/TryFuse가 성공할 때마다 발행하는
        // InventoryChangedEvent를 OnInventoryChanged가 이미 구독해서 Refresh()하고 있다.
        private void EnhanceAll()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentEnhancementService enhancementService))
            {
                enhancementService.TryEnhanceAll(_openSlot);
            }
        }

        private void FuseAll()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentFusionService fusionService))
            {
                fusionService.TryFuseAll(_openSlot);
            }
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            if (_isOpen)
            {
                _isRefreshPending = true;
            }
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            if (_isOpen)
            {
                _isRefreshPending = true;
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
                row.Initialize(owned, owned == currentlyEquipped, backgroundColor, target => detailPopup?.Open(target, currentlyEquipped), target => enhancementPopup?.Open(target));

                _spawnedRows.Add(row);
            }
        }
    }
}
