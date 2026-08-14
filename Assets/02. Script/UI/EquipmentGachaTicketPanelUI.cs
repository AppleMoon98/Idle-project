using System.Collections.Generic;
using Core;
using Equipment;
using Gacha;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "무기 뽑기" 카테고리 안, 티켓 뽑기 티어 패널. EquipmentGachaTierPanelUI(골드
    /// 뽑기)와 대칭되는 구조지만 슬롯 선택이 없다(무기 슬롯 전용) - 골드 대신
    /// EquipmentGachaTicketService의 보유 뽑기권을 표시/소비한다. 뽑기 결과는 팝업
    /// (EquipmentPulledPopup, 이제 미사용) 대신 resultReveal이 이 패널 안에서 슬롯으로 하나씩
    /// 보여준다.
    /// </summary>
    public sealed class EquipmentGachaTicketPanelUI : MonoBehaviour
    {
        private static readonly Color NeutralBorderColor = new(0.35f, 0.35f, 0.35f, 1f);

        [SerializeField]
        private Text ticketText;

        [SerializeField]
        private Button[] pullButtons;

        [SerializeField]
        private int[] pullCounts;

        [SerializeField]
        private GachaResultRevealController resultReveal;

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
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EquipmentGachaService gacha))
            {
                return;
            }

            IReadOnlyList<EquipmentSO> results = gacha.PullWithTicket(count);
            resultReveal.Reveal(BuildVisuals(results));
        }

        private static List<GachaResultVisual> BuildVisuals(IReadOnlyList<EquipmentSO> results)
        {
            var visuals = new List<GachaResultVisual>(results.Count);

            foreach (EquipmentSO item in results)
            {
                Color border = item.Grade != null ? item.Grade.TintColor : NeutralBorderColor;
                visuals.Add(new GachaResultVisual(item.Icon, border));
            }

            return visuals;
        }

        private void SetTicketText(int amount)
        {
            ticketText.text = $"티켓: {amount}";
        }
    }
}
