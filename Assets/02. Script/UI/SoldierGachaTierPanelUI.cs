using Core;
using Gacha;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "병사 뽑기" 카테고리 안, 티어 하나(일반/고급/유료 등)의 패널. 이 패널이 몇 번째
    /// 티어를 대표하는지는 tierIndex로 지정하고, GachaService.Tiers[tierIndex]에서 비용/표시
    /// 이름을 그대로 읽어온다 — 티어가 늘어나도 이 스크립트는 손댈 필요가 없다.
    /// 소환권을 얻을 정식 경로가 아직 없어 디버그 지급 버튼으로 테스트한다(추후 진짜 획득
    /// 시스템이 생기면 이 버튼만 제거하면 된다).
    /// </summary>
    public sealed class SoldierGachaTierPanelUI : MonoBehaviour
    {
        [SerializeField]
        private int tierIndex;

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
                gacha.TryPull(tierIndex, out _);
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
