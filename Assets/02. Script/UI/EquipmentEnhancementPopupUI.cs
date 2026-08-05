using System.Collections.Generic;
using Core;
using Enhancement;
using Equipment;
using Inventory;
using Inventory.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 장비 한 라인을 강화하는 팝업. 아이콘/성공 확률(현재는 항상 100%)/강화 단계 변화/
    /// 능력치별 현재→다음 값/필요 재료를 보여주고, 강화 버튼은 EquipmentEnhancementService.TryEnhance를
    /// 호출한다. 강화 성공은 InventoryChangedEvent를 발행하므로(EquipmentEnhancementService가 내부적으로
    /// InventoryService.AddEnhancementLevel을 호출), 이 팝업은 그 이벤트를 구독해 새로고침만 하면 되고
    /// 버튼 클릭 핸들러가 직접 화면을 갱신할 필요는 없다.
    /// </summary>
    public sealed class EquipmentEnhancementPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Text probabilityText;

        [SerializeField]
        private Text levelText;

        [SerializeField]
        private Transform statRowContainer;

        [SerializeField]
        private EquipmentStatPreviewRowUI statRowPrefab;

        [SerializeField]
        private Text materialText;

        [SerializeField]
        private Button enhanceButton;

        [SerializeField]
        private Text enhanceButtonLabel;

        private OwnedEquipment _target;
        private bool _isOpen;
        private readonly List<EquipmentStatPreviewRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
            enhanceButton.onClick.AddListener(OnEnhanceClicked);
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
        /// target 라인의 강화 팝업을 연다.
        /// </summary>
        public void Open(OwnedEquipment target)
        {
            _target = target;
            _isOpen = true;
            popupRoot.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// 팝업을 닫는다. 부모 팝업(EquipmentSlotPopupUI)이 닫힐 때 같이 닫기 위해 외부에서도 호출한다.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            if (_isOpen && evt.Changed == _target)
            {
                Refresh();
            }
        }

        private void OnEnhanceClicked()
        {
            if (_target == null || GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out EquipmentEnhancementService enhancement))
            {
                return;
            }

            enhancement.TryEnhance(_target.Definition);
        }

        private void Refresh()
        {
            foreach (EquipmentStatPreviewRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (_target == null || _target.Count <= 0)
            {
                Close();
                return;
            }

            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out EquipmentEnhancementService enhancement)
                || !GameBootstrapper.Services.TryGet(out EquipmentStatService statService))
            {
                return;
            }

            EquipmentSO definition = _target.Definition;
            int currentLevel = _target.EnhancementLevel;
            bool isMax = currentLevel >= enhancement.MaxLevel;

            iconImage.sprite = definition.Icon;

            probabilityText.text = "성공 확률: 100%";

            levelText.text = isMax
                ? $"{currentLevel}강 (MAX)"
                : $"{currentLevel}강 → {currentLevel + 1}강";

            IReadOnlyList<(EnhancementStatType StatType, float Bonus)> currentStats =
                statService.CalculatePreview(definition, currentLevel);
            IReadOnlyList<(EnhancementStatType StatType, float Bonus)> nextStats = isMax
                ? currentStats
                : statService.CalculatePreview(definition, currentLevel + 1);

            foreach ((EnhancementStatType statType, float currentBonus) in currentStats)
            {
                EquipmentStatPreviewRowUI row = Instantiate(statRowPrefab, statRowContainer);
                row.Initialize(statType, currentBonus, StatOptionLookup.FindBonus(nextStats, statType));
                _spawnedRows.Add(row);
            }

            materialText.text = isMax
                ? "-"
                : $"필요 재료: 중복 장비 {enhancement.DuplicatesRequiredPerLevel}개 + 강화석 {enhancement.GetNextStoneCost(definition)}";

            enhanceButton.interactable = !isMax;
            enhanceButtonLabel.text = isMax ? "MAX" : "강화하기";
        }
    }
}
