using Core;
using SoldierEquipment;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 장비 팝업의 아이템 한 줄(행)을 표시/제어한다. 보유하지 않은 아이템도 개수 0으로
    /// 표시되며(SoldierEquipmentCatalogSO 전체 기준 나열), 지급 경로가 아직 없어 디버그
    /// 지급 버튼으로 테스트한다(추후 진짜 획득 시스템이 생기면 이 버튼만 제거하면 됨).
    /// </summary>
    public sealed class SoldierEquipmentRowUI : MonoBehaviour
    {
        [SerializeField]
        private Text label;

        [SerializeField]
        private Button equipButton;

        [SerializeField]
        private Button unequipButton;

        [SerializeField]
        private Button debugGrantButton;

        /// <summary>
        /// 행 데이터를 채운다. instanceId는 지금 장비 팝업이 열려 있는 병사 유닛이다. 장착/지급은
        /// SoldierEquippedGearService/SoldierEquipmentInventoryService의 이벤트를 SoldierEquipmentPopupUI가
        /// 이미 구독하고 있으므로, 여기서는 서비스만 호출하고 새로고침은 그 구독에 맡긴다
        /// (EquipmentRowUI의 합성/강화 버튼과 동일한 방식). 장착은 재고를 1개 소모하므로
        /// ownedCount는 이미 다른 병사가 착용 중인 수량은 제외한 "남은 여유분"이다.
        /// </summary>
        public void Initialize(SoldierEquipmentSO definition, int ownedCount, bool isEquipped, int instanceId)
        {
            string equippedTag = isEquipped ? "✓ " : "";
            label.text = $"{equippedTag}{definition.ItemName} x{ownedCount}";

            equipButton.interactable = !isEquipped && ownedCount > 0;
            equipButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null
                    && GameBootstrapper.Services.TryGet(out SoldierEquipmentInventoryService inventory)
                    && GameBootstrapper.Services.TryGet(out SoldierEquippedGearService equippedGear)
                    && inventory.TryGet(definition, out OwnedSoldierEquipment owned))
                {
                    equippedGear.Equip(instanceId, owned);
                }
            });

            unequipButton.interactable = isEquipped;
            unequipButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierEquippedGearService equippedGear))
                {
                    equippedGear.Unequip(instanceId, definition.SlotType);
                }
            });

            debugGrantButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierEquipmentInventoryService inventory))
                {
                    inventory.AddSoldierEquipment(definition, 1);
                }
            });
        }
    }
}
