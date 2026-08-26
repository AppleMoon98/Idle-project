using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SoldierDeploymentPanelUI(배치 슬롯 목록)를 감싸는 팝업 셸. SoldierPanel 하단에 항상 떠 있던
    /// 배치 슬롯 스크롤 목록을 패널 상단의 "배치 관리" 버튼으로 여는 팝업으로 옮기면서 추가했다 —
    /// SoldierDeploymentPanelUI 자체는 손대지 않았다: 그 컴포넌트가 붙은 DeploymentContainer가
    /// popupRoot(PopupPanel)의 자식으로 들어가 있어서, 이 컴포넌트가 popupRoot를 SetActive할 때
    /// DeploymentContainer의 OnEnable/OnDisable이 그대로 함께 트리거되어 기존 새로고침/구독
    /// 로직이 별도 코드 없이 그대로 작동한다.
    /// </summary>
    public sealed class SoldierDeploymentPopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button closeButton;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

        /// <summary>
        /// 팝업을 닫는다. 여는 버튼이 있는 쪽에서 필요하면 이 메서드로 직접 닫을 수도 있게 공개해둔다.
        /// </summary>
        public void Close()
        {
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
