using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 여닫기만 하는 범용 팝업 셸. 통합 메뉴 팝업, 던전 팝업처럼 아직 내부 콘텐츠가
    /// 없거나 단순히 열림/닫힘 상태만 필요한 팝업에 재사용한다(GachaPopupUI와 동일한
    /// 모양이 세 번째로 필요해져서 클래스로 뽑음). 도메인 지식은 전혀 없으며,
    /// 인스펙터에서 popupRoot/openButton/closeButton만 연결하면 동작한다.
    /// </summary>
    public sealed class SimplePopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        /// <summary>
        /// 이 팝업이 열릴 때 함께 닫을 다른 SimplePopupUI(선택, 기본값 null).
        /// 예: 통합 메뉴 팝업 안의 버튼을 눌러 다른 전체화면 팝업으로 전환할 때,
        /// 메뉴 팝업 쪽에 이 필드로 대상 팝업을 지정하는 대신, 대상 팝업 쪽에
        /// "열릴 때 메뉴 팝업을 닫는다"를 지정한다 - Close()를 그대로 거치므로
        /// BackNavigationService 등록/해제도 함께 정상 처리된다.
        /// </summary>
        [SerializeField]
        private SimplePopupUI popupToCloseOnOpen;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            popupToCloseOnOpen?.Close();
            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

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
