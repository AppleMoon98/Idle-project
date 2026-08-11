using Core;
using Soldier;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 로스터 슬롯(SoldierRosterRowUI)을 탭하면 뜨는 작은 선택 팝업 — 배치/장비/행동 버튼을 갖는다.
    /// 장비/행동은 각각 SoldierEquipmentPopupUI/SoldierBehaviorProfilePopupUI를 그 유닛의
    /// InstanceId로 열고 자신은 닫는다(UI.EquippedSlotBarUI→EquipmentSlotPopupUI와 같은 "슬롯
    /// 탭 → 팝업" 체인의 병사 로스터 버전). 배치는 별도 팝업 없이 이 자리에서 바로 처리한다 —
    /// 기존에는 배치 슬롯 쪽에서 SoldierDeploymentPickerPopupUI로 "슬롯을 고르고 → 병사를 고르는"
    /// 역방향 흐름만 있어서, 로스터에서 특정 병사를 보다가 바로 배치하려면 배치 탭으로 이동해야
    /// 하는 불편함이 있었다. 이미 배치돼 있으면 버튼이 "배치 해제"로 바뀌어 토글처럼 동작한다.
    /// </summary>
    public sealed class SoldierRosterSlotActionPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text nameLabel;

        [SerializeField]
        private Button deployButton;

        [SerializeField]
        private Text deployButtonLabel;

        [SerializeField]
        private Button equipmentButton;

        [SerializeField]
        private Button behaviorButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private SoldierEquipmentPopupUI equipmentPopup;

        [SerializeField]
        private SoldierBehaviorProfilePopupUI behaviorProfilePopup;

        private int _instanceId;

        private void Awake()
        {
            popupRoot.SetActive(false);
            deployButton.onClick.AddListener(OnDeployClicked);
            equipmentButton.onClick.AddListener(OnEquipmentClicked);
            behaviorButton.onClick.AddListener(OnBehaviorClicked);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// owned 유닛을 대상으로 팝업을 연다.
        /// </summary>
        public void Open(OwnedSoldier owned)
        {
            _instanceId = owned.InstanceId;
            nameLabel.text = owned.Definition.DisplayName;
            RefreshDeployButtonLabel();
            popupRoot.SetActive(true);
        }

        /// <summary>
        /// 팝업을 닫는다. SoldierRosterPanelUI가 비활성화될 때 같이 닫을 수 있도록 공개해둔다.
        /// </summary>
        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void RefreshDeployButtonLabel()
        {
            bool isDeployed = GameBootstrapper.Services != null
                && GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment)
                && deployment.TryGetSlotOf(_instanceId, out _);

            deployButtonLabel.text = isDeployed ? "배치 해제" : "배치";
        }

        /// <summary>
        /// 이미 배치돼 있으면 그 슬롯에서 해제한다. 배치돼 있지 않으면 현재 랭크로 잠금 해제된
        /// 슬롯 중 비어있는 첫 번째 슬롯에 배정한다. 빈 슬롯이 하나도 없으면 토스트로 안내하고
        /// 아무 슬롯도 건드리지 않는다(다른 병사를 임의로 밀어내지 않음).
        /// </summary>
        private void OnDeployClicked()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            if (deployment.TryGetSlotOf(_instanceId, out int currentSlot))
            {
                deployment.Unassign(currentSlot);
                RefreshDeployButtonLabel();
                return;
            }

            int maxSlots = deployment.GetMaxUnlockedSlotCount();

            for (int slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            {
                if (deployment.TryGetAssigned(slotIndex, out _))
                {
                    continue;
                }

                deployment.TryAssign(slotIndex, _instanceId);
                RefreshDeployButtonLabel();
                return;
            }

            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("배치 슬롯이 가득 찼습니다."));
        }

        private void OnEquipmentClicked()
        {
            equipmentPopup.Open(_instanceId);
            Close();
        }

        private void OnBehaviorClicked()
        {
            behaviorProfilePopup.Open(_instanceId);
            Close();
        }
    }
}
