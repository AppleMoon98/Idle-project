using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 전체를 덮는 가챠 팝업의 열기/닫기만 담당한다. 내부의 카테고리 탭(무기/병사)과
    /// 각 카테고리 안의 티어 탭(일반/고급/유료)은 전부 BottomMenuUI 인스턴스를 중첩해서
    /// 구성하므로, 이 스크립트는 그 탭 전환 로직을 전혀 알 필요가 없다. 하단 탭 바의 다른
    /// 4개 버튼과 달리 이 팝업은 하단 탭 바 자체까지 덮어야 해서 BottomMenuUI의 패널 배열에
    /// 들어가지 않고, 여는 버튼을 직접 구독한다.
    /// </summary>
    public sealed class GachaPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        private void Awake()
        {
            popupRoot.SetActive(false);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
        }

        private void Open()
        {
            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
