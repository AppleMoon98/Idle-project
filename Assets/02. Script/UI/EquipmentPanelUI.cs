using System.Text;
using Core;
using Inventory;
using Inventory.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// InventoryService가 보관 중인 장비 목록을 텍스트로 표시한다.
    /// </summary>
    public sealed class EquipmentPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Text listText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out InventoryService inventoryService))
            {
                return;
            }

            var sb = new StringBuilder();

            foreach (Inventory.OwnedEquipment owned in inventoryService.Items)
            {
                string gradeName = owned.Definition.Grade != null ? owned.Definition.Grade.DisplayName : "-";
                sb.AppendLine($"{owned.Definition.ItemName} ({owned.Definition.EquipmentType}, {gradeName}) x{owned.Count}");
            }

            listText.text = sb.Length > 0 ? sb.ToString() : "보유한 장비가 없습니다.";
        }
    }
}
