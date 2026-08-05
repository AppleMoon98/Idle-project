using System.Collections.Generic;
using System.Text;
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
    /// 탭한 장비가 주는 능력치 옵션 목록과, 그 슬롯에 현재 장착 중인 장비 대비 증감(▲/▼)을
    /// 보여주는 상세 팝업. EquipmentSlotPopupUI 위에 뜨는 자식 팝업이라, 부모 팝업이 닫힐 때
    /// 같이 닫혀야 한다(EquippedSlotBarUI가 EquipmentSlotPopupUI.Close()를 관리하는 것과 같은 패턴).
    /// </summary>
    public sealed class EquipmentDetailPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text nameText;

        [SerializeField]
        private Text optionsText;

        [SerializeField]
        private Button closeButton;

        private OwnedEquipment _target;
        private OwnedEquipment _currentlyEquipped;
        private bool _isOpen;

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
        /// target의 옵션 목록을 보여준다. currentlyEquipped가 있고 target과 다르면 능력치별 증감을
        /// 같이 표시한다(같은 슬롯이므로 두 아이템이 주는 능력치 종류는 항상 동일하다).
        /// </summary>
        public void Open(OwnedEquipment target, OwnedEquipment currentlyEquipped)
        {
            if (target == null)
            {
                return;
            }

            _target = target;
            _currentlyEquipped = currentlyEquipped;
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
            if (!_isOpen)
            {
                return;
            }

            if (evt.Changed != _target && evt.Changed != _currentlyEquipped)
            {
                return;
            }

            if (_target.Count <= 0)
            {
                Close();
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EquipmentStatService statService))
            {
                return;
            }

            string gradeName = _target.Definition.Grade != null ? _target.Definition.Grade.DisplayName : "-";
            nameText.text = $"{_target.Definition.ItemName} ({gradeName}, 강화 {_target.EnhancementLevel})";

            IReadOnlyList<(EnhancementStatType StatType, float Bonus)> targetOptions =
                statService.CalculatePreview(_target.Definition, _target.EnhancementLevel);

            bool hasComparison = _currentlyEquipped != null && _currentlyEquipped != _target;
            IReadOnlyList<(EnhancementStatType StatType, float Bonus)> comparisonOptions = hasComparison
                ? statService.CalculatePreview(_currentlyEquipped.Definition, _currentlyEquipped.EnhancementLevel)
                : null;

            var sb = new StringBuilder();

            foreach ((EnhancementStatType statType, float bonus) in targetOptions)
            {
                sb.Append(StatDisplayNames.Get(statType));
                sb.Append(' ');
                sb.Append(bonus.ToString("0.##"));

                if (hasComparison)
                {
                    sb.Append(' ');
                    sb.Append(FormatDiff(bonus - StatOptionLookup.FindBonus(comparisonOptions, statType)));
                }

                sb.AppendLine();
            }

            optionsText.text = sb.Length > 0 ? sb.ToString() : "옵션 없음";
        }

        private static string FormatDiff(float diff)
        {
            if (Mathf.Approximately(diff, 0f))
            {
                return "(-)";
            }

            return diff > 0f ? $"(▲+{diff:0.##})" : $"(▼{diff:0.##})";
        }
    }
}
