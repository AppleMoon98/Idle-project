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

        private int _activeIndex = -1;

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
        }

        private void OnTabClicked(int index)
        {
            if (_activeIndex == index)
            {
                panels[index].SetActive(false);
                _activeIndex = -1;
                return;
            }

            if (_activeIndex >= 0)
            {
                panels[_activeIndex].SetActive(false);
            }

            panels[index].SetActive(true);
            _activeIndex = index;
        }
    }
}
