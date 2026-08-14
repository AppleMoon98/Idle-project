using Core;
using Gacha;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "무기 뽑기" 카테고리 안, 티켓 뽑기 티어 패널. EquipmentGachaTierPanelUI(골드
    /// 뽑기)와 대칭되는 구조지만 슬롯 선택이 없다(무기 슬롯 전용) - 골드 대신
    /// EquipmentGachaTicketService의 보유 뽑기권을 표시/소비한다.
    /// </summary>
    public sealed class EquipmentGachaTicketPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Text ticketText;

        [SerializeField]
        private Button[] pullButtons;

        [SerializeField]
        private int[] pullCounts;

        private void Awake()
        {
            for (int i = 0; i < pullButtons.Length; i++)
            {
                int count = pullCounts[i];
                pullButtons[i].onClick.AddListener(() => OnPullClicked(count));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<EquipmentGachaTicketChangedEvent>(OnTicketChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentGachaTicketService ticketService))
            {
                SetTicketText(ticketService.CurrentTickets);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<EquipmentGachaTicketChangedEvent>(OnTicketChanged);
        }

        private void OnTicketChanged(EquipmentGachaTicketChangedEvent evt)
        {
            SetTicketText(evt.CurrentTickets);
        }

        private void OnPullClicked(int count)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentGachaService gacha))
            {
                gacha.PullWithTicket(count);
            }
        }

        private void SetTicketText(int amount)
        {
            ticketText.text = $"티켓: {amount}";
        }
    }
}
