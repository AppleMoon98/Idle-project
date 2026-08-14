using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 하단 탭 버튼과 그에 대응하는 패널을 토글/전환하는 범용 네비게이션 셸.
    /// 각 패널이 무엇을 표시하는지는 전혀 모른다.
    /// </summary>
    public sealed class BottomMenuUI : MonoBehaviour
    {
        [SerializeField]
        private Button[] tabButtons;

        [SerializeField]
        private GameObject[] panels;

        [SerializeField]
        private bool selectFirstTabByDefault;

        [SerializeField]
        private bool allowToggleClose = true;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
            }

            foreach (GameObject panel in panels)
            {
                panel.SetActive(false);
            }

            if (selectFirstTabByDefault && panels.Length > 0)
            {
                panels[0].SetActive(true);
            }
        }

        // 어느 탭이 열려있는지를 별도 인덱스로 캐싱하지 않고 panels[].activeSelf를 그때그때
        // 직접 읽는다 - 패널이 BottomMenuUI를 거치지 않고 외부에서 직접 SetActive(false)되는
        // 경우(예: EquipmentSlotPopupUI.Close()가 팝업 상단 닫기 버튼에서 equippedSlotBar를
        // 직접 끄는 것)가 있어, 캐싱된 인덱스만 믿으면 실제 상태와 어긋나 다음 탭 클릭이
        // "이미 열려있다고 착각하고 닫기"로 처리돼 한 번 더 눌러야 열리는 문제가 생긴다.
        private void OnTabClicked(int index)
        {
            if (panels[index].activeSelf)
            {
                if (!allowToggleClose)
                {
                    return;
                }

                panels[index].SetActive(false);
                return;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                if (i != index)
                {
                    panels[i].SetActive(false);
                }
            }

            panels[index].SetActive(true);
        }
    }
}
