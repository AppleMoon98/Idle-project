using Core;
using Gacha;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 보유 병사 소환권을 표시하고, 뽑기/디버그 지급 버튼을 제공한다. 소환권을 얻을 정식 경로가
    /// 아직 없어 디버그 지급 버튼으로 테스트한다(추후 진짜 획득 시스템이 생기면 이 버튼만 제거하면 됨).
    /// </summary>
    public sealed class SoldierTicketDisplayUI : MonoBehaviour
    {
        [SerializeField]
        private Text ticketText;

        [SerializeField]
        private Button pullButton;

        [SerializeField]
        private Button debugGrantButton;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierTicketChangedEvent>(OnTicketChanged);
            pullButton.onClick.AddListener(OnPullClicked);
            debugGrantButton.onClick.AddListener(OnDebugGrantClicked);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierTicketService tickets))
            {
                SetTicketText(tickets.CurrentTickets);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierTicketChangedEvent>(OnTicketChanged);
            pullButton.onClick.RemoveListener(OnPullClicked);
            debugGrantButton.onClick.RemoveListener(OnDebugGrantClicked);
        }

        private void OnTicketChanged(SoldierTicketChangedEvent evt)
        {
            SetTicketText(evt.CurrentTickets);
        }

        private void OnPullClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GachaService gacha))
            {
                gacha.TryPull(out _);
            }
        }

        private void OnDebugGrantClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierTicketService tickets))
            {
                tickets.AddTickets(1);
            }
        }

        private void SetTicketText(int amount)
        {
            ticketText.text = $"소환권: {amount}";
        }
    }
}
