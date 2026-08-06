using Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 장비 뽑기에서 어느 슬롯(무기/장갑/갑옷/투구/신발)을 뽑을지 고르는 드롭다운.
    /// 여는 트리거는 이 컴포넌트가 속한 카테고리 탭 버튼("장비 뽑기") 자체다 — 별도의
    /// 트리거 버튼 없이, 그 탭을 누를 때마다 optionsContainer(5개 슬롯 버튼)가 티어
    /// 탭/뽑기 버튼 위를 덮는 오버레이로 열린다. slotButtons는 EquipmentType 선언 순서
    /// (무기/장갑/갑옷/투구/신발)와 정확히 같은 순서로 구성돼 있어, 클릭된 인덱스를 그대로
    /// EquipmentType으로 캐스팅한다. 옵션을 고르면 즉시 닫히므로(드롭다운 동작),
    /// EquipmentGachaTierPanelUI는 뽑기 버튼을 누르는 시점에 SelectedSlot만 읽어가면 된다.
    /// </summary>
    public sealed class EquipmentSlotSelectorUI : MonoBehaviour
    {
        [SerializeField]
        private Button categoryTabButton;

        [SerializeField]
        private GameObject optionsContainer;

        [SerializeField]
        private Button[] slotButtons;

        [SerializeField]
        private Color selectedColor = new Color(0.55f, 0.85f, 0.95f, 1f);

        [SerializeField]
        private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        /// <summary>
        /// 현재 선택된 슬롯. 기본값 Weapon(첫 번째 버튼과 동일).
        /// </summary>
        public EquipmentType SelectedSlot { get; private set; } = EquipmentType.Weapon;

        private void Awake()
        {
            categoryTabButton.onClick.AddListener(Open);

            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slotIndex = i;
                slotButtons[i].onClick.AddListener(() => Select(slotIndex));
            }

            Select(0);
            optionsContainer.SetActive(false);
        }

        /// <summary>
        /// categoryTabButton을 누를 때마다 항상 목록을 연다(토글 아님). optionsContainer 자신의
        /// activeSelf를 뒤집는 토글 방식은, 이 버튼이 카테고리 패널 자체의 표시 여부도 함께
        /// 제어하는 탓에(다른 카테고리 탭으로 전환했다가 되돌아오는 경우 등) 패널의 실제 표시
        /// 상태와 optionsContainer의 activeSelf가 서로 어긋나는 문제가 있었다. 항상 열기로
        /// 단순화하면 이 불일치가 애초에 발생하지 않는다.
        /// </summary>
        private void Open()
        {
            optionsContainer.SetActive(true);
        }

        private void Select(int index)
        {
            SelectedSlot = (EquipmentType)index;

            for (int i = 0; i < slotButtons.Length; i++)
            {
                slotButtons[i].image.color = i == index ? selectedColor : normalColor;
            }

            optionsContainer.SetActive(false);
        }
    }
}
