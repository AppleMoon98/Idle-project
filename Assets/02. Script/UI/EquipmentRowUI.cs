using System;
using Core;
using Equipment;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 장착 팝업의 장비 한 줄(행)을 표시/제어한다. EquipmentSlotPopupUI가 보유 장비 개수만큼
    /// 이 컴포넌트가 붙은 프리팹을 Instantiate하고 Initialize로 데이터를 채운다.
    /// </summary>
    public sealed class EquipmentRowUI : MonoBehaviour
    {
        [SerializeField]
        private Image background;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Button rowButton;

        [SerializeField]
        private Button fuseButton;

        [SerializeField]
        private Button enhanceButton;

        private OwnedEquipment _owned;

        /// <summary>
        /// 행 데이터를 채운다. onEquipped는 장착(행 클릭) 성공 후 팝업을 닫는 등
        /// 호출자가 처리할 후속 동작을 위한 콜백이다.
        /// </summary>
        public void Initialize(OwnedEquipment owned, bool isEquipped, Color backgroundColor, Action onEquipped)
        {
            _owned = owned;
            background.color = backgroundColor;

            string equippedTag = isEquipped ? "✓ " : "";
            label.text = $"{equippedTag}{owned.Definition.ItemName} x{owned.Count} (강화 {owned.EnhancementLevel})";

            rowButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquippedGearService equippedGear))
                {
                    equippedGear.Equip(_owned);
                    onEquipped?.Invoke();
                }
            });

            fuseButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentFusionService fusion))
                {
                    fusion.TryFuse(_owned.Definition);
                }
            });

            enhanceButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentEnhancementService enhancement))
                {
                    enhancement.TryEnhance(_owned.Definition);
                }
            });
        }
    }
}
