using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Equipment;
using Inventory;
using Inventory.Events;
using UI.Events;
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
        private Button sortButton;

        [SerializeField]
        private Text sortButtonLabel;

        [SerializeField]
        private Button enhanceAllButton;

        [SerializeField]
        private Button fuseAllButton;

        [SerializeField]
        private EquipmentDetailPopupUI detailPopup;

        [SerializeField]
        private EquipmentEnhancementPopupUI enhancementPopup;

        [SerializeField]
        private ConfirmationPopupUI confirmationPopup;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        [SerializeField]
        private EquipmentCatalogSO equipmentCatalog;

        [SerializeField]
        private GameObject equippedSlotBar;

        [SerializeField]
        private Color cardBaseColor = new Color(0.13f, 0.10f, 0.08f, 0.92f);

        [SerializeField]
        [Range(0f, 1f)]
        private float gradeTintBlend = 0.35f;

        // 순수 클라이언트 선호도라 서비스/이벤트 왕복 없이 직접 PlayerPrefs를 읽고 쓴다
        // (CameraShakeToggleUI/ConfirmationPopupUI와 같은 관례) - 앱을 재시작해도 유지된다.
        private const string SortDescendingPrefsKey = "EquipmentSlotPopup.SortDescending";

        private EquipmentType _openSlot;
        private bool _isOpen;
        private bool _isRefreshPending;

        // 팝업이 닫혔다 다시 열려도(EquippedSlotBarUI가 매번 Open()을 다시 부른다) 초기화되면
        // 안 되므로, Awake 이후 이 필드를 건드리는 곳은 ToggleSort 하나뿐이다 - Open()/Awake()
        // 어디에서도 리셋하지 않는다. 초기값은 Awake()가 PlayerPrefs에서 읽어온다.
        private bool _sortDescending;

        private readonly List<EquipmentRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            _sortDescending = PlayerPrefs.GetInt(SortDescendingPrefsKey, 0) != 0;
            sortButton.onClick.AddListener(ToggleSort);
            enhanceAllButton.onClick.AddListener(EnhanceAll);
            fuseAllButton.onClick.AddListener(FuseAll);
            UpdateSortButtonLabel();
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
        /// 팝업을 닫는다. 이 팝업 자체에는 닫기 버튼이 없다(장비 탭을 다시 누르면
        /// EquippedSlotBarUI.OnDisable()이 이 메서드를 호출해 닫는다) - 대신 여기서
        /// equippedSlotBar도 함께 SetActive(false)해, 장비 탭이 열려있는 다른 경로(예: 다음
        /// 세션에서 닫기 동작이 다시 추가되는 경우)로 이 메서드가 불려도 슬롯바가 팝업 없이
        /// 혼자 남지 않게 한다.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
            detailPopup?.Close();
            enhancementPopup?.Close();
            equippedSlotBar?.SetActive(false);
        }

        // 목록 갱신은 별도로 호출할 필요가 없다 - TryEnhance/TryFuse가 성공할 때마다 발행하는
        // InventoryChangedEvent를 OnInventoryChanged가 이미 구독해서 Refresh()하고 있다.
        //
        // 전체 강화/전체 합성/개별 합성 셋 다 실행 직전에 ConfirmationPopupUI로 확인을 받는다
        // (개별 강화는 EquipmentEnhancementPopupUI 자신의 강화 버튼에서 같은 방식으로 확인받는다).
        // confirmationPopup을 못 구하면(씬 배선 누락 등 방어적 상황) 확인 없이 즉시 실행한다.
        private void EnhanceAll()
        {
            RequestConfirm("EnhanceAll", "전체 강화를 진행합니다. 정말로 진행하시겠습니까?", () =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentEnhancementService enhancementService))
                {
                    enhancementService.TryEnhanceAll(_openSlot);
                }
            });
        }

        private void FuseAll()
        {
            RequestConfirm("FuseAll", "전체 합성을 진행합니다. 정말로 진행하시겠습니까?", () =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentFusionService fusionService))
                {
                    fusionService.TryFuseAll(_openSlot);
                }
            });
        }

        private void RequestFuseConfirm(OwnedEquipment owned)
        {
            RequestConfirm("Fuse", "합성을 진행합니다. 정말로 진행하시겠습니까?", () =>
            {
                if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EquipmentFusionService fusionService))
                {
                    return;
                }

                if (fusionService.TryFuse(owned.Definition))
                {
                    return;
                }

                // 다음 등급 자체가 없는 실패(이미 최고 등급/콘텐츠 미비)는 재료 부족과 무관하므로
                // 여기서 걸러낸다 - 다음 등급이 실제로 존재할 때만 부족분 안내를 띄운다.
                EquipmentGradeSO nextGrade = gradeCatalog.GetNext(owned.Definition.Grade);
                bool hasNextItem = nextGrade != null && equipmentCatalog.FindBySlotAndGrade(owned.Definition.EquipmentType, nextGrade) != null;
                int shortage = fusionService.RequiredCount - owned.Count;

                if (hasNextItem && shortage > 0)
                {
                    GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent($"{owned.Definition.ItemName}가 {shortage}개 부족합니다."));
                }
            });
        }

        private void RequestConfirm(string actionKey, string message, Action onConfirm)
        {
            if (confirmationPopup != null)
            {
                confirmationPopup.RequestConfirm(actionKey, message, onConfirm);
            }
            else
            {
                onConfirm();
            }
        }

        /// <summary>
        /// 등급 정렬 방향(오름차순/내림차순)만 뒤집는다. "보유한 라인이 항상 맨 위"라는 1순위
        /// 기준은 Refresh()에서 이 필드와 무관하게 항상 적용되므로 여기서는 건드리지 않는다.
        /// </summary>
        private void ToggleSort()
        {
            _sortDescending = !_sortDescending;
            PlayerPrefs.SetInt(SortDescendingPrefsKey, _sortDescending ? 1 : 0);
            PlayerPrefs.Save();
            UpdateSortButtonLabel();

            if (_isOpen)
            {
                Refresh();
            }
        }

        private void UpdateSortButtonLabel()
        {
            if (sortButtonLabel != null)
            {
                sortButtonLabel.text = _sortDescending ? "정렬 ▼" : "정렬 ▲";
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
                || !GameBootstrapper.Services.TryGet(out EquippedGearService equippedGear)
                || equipmentCatalog == null)
            {
                return;
            }

            OwnedEquipment currentlyEquipped = equippedGear.GetEquipped(_openSlot);

            // 보유한 라인뿐 아니라 이 슬롯에 존재하는 장비 원형 전체를 나열한다 - 한 번도 획득하지
            // 못한 장비도 "무엇이 있는지" 미리 보여주기 위함(EquipmentRowUI가 owned==null이면
            // 비활성 상태로 그린다). 개수 0인 라인은 InventoryService가 더 이상 지우지 않으므로
            // inventory.TryGet으로 정상적으로 조회된다.
            //
            // 정렬 1순위는 항상 "보유 여부"다(_sortDescending과 무관) - 보유한 라인이 항상 위에
            // 오고, 그 안에서만 등급 오름차순/내림차순이 _sortDescending에 따라 갈린다.
            IEnumerable<(EquipmentSO Definition, OwnedEquipment Owned)> entries = equipmentCatalog.Items
                .Where(item => item != null && item.EquipmentType == _openSlot)
                .Select(item =>
                {
                    inventory.TryGet(item, out OwnedEquipment owned);
                    return (Definition: item, Owned: owned);
                });

            IOrderedEnumerable<(EquipmentSO Definition, OwnedEquipment Owned)> ordered = entries
                .OrderBy(entry => entry.Owned == null ? 1 : 0);

            ordered = _sortDescending
                ? ordered.ThenByDescending(entry => gradeCatalog.IndexOf(entry.Definition.Grade))
                : ordered.ThenBy(entry => gradeCatalog.IndexOf(entry.Definition.Grade));

            foreach ((EquipmentSO definition, OwnedEquipment owned) in ordered)
            {
                EquipmentRowUI row = Instantiate(rowPrefab, rowContainer);
                Color backgroundColor = EquipmentRowUI.ComputeGradeBackground(cardBaseColor, definition.Grade, gradeTintBlend);
                row.Initialize(definition, owned, owned != null && owned == currentlyEquipped, backgroundColor, target => detailPopup?.Open(target, currentlyEquipped), target => enhancementPopup?.Open(target), RequestFuseConfirm);

                _spawnedRows.Add(row);
            }
        }
    }
}
