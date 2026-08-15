using System.Collections.Generic;
using Core;
using Gacha;
using Gacha.Events;
using Loot;
using Loot.Events;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "병사 뽑기" 카테고리 안, 티어 하나(골드/티켓/픽업 등)의 패널. 이 패널이 몇 번째
    /// 티어를 대표하는지는 tierIndex로 지정하고, GachaService.Tiers[tierIndex]에서 비용/표시
    /// 이름을 그대로 읽어온다 — 티어가 늘어나도 이 스크립트는 손댈 필요가 없다. pullButtons[i]는
    /// pullCounts[i]개를 한 번에 뽑는 버튼이다(1/10/30개 등, 배열 길이만큼 자유롭게 구성).
    /// useGoldCurrency로 이 패널 인스턴스가 골드/티켓 중 어느 재화를 표시할지 정한다(같은 클래스를
    /// 골드 탭/티켓 탭 인스턴스 둘 다에 재사용 — EquipmentGachaTierPanelUI가 이미 골드만 쓰던
    /// 것과 이 패널의 기존 티켓 표시를 한 컴포넌트로 합친 것). 소환권을 얻을 정식 경로가 아직
    /// 없어 디버그 지급 버튼으로 테스트한다(골드 탭은 이미 실제 골드 재화가 있어 필요 없음). 뽑기
    /// 결과는 팝업(SoldierPulledPopup, 이제 미사용) 대신 resultReveal이 이 패널 안에서 슬롯으로
    /// 하나씩 보여준다.
    /// </summary>
    public sealed class SoldierGachaTierPanelUI : MonoBehaviour
    {
        private static readonly Color NeutralBorderColor = new(0.35f, 0.35f, 0.35f, 1f);

        [SerializeField]
        private int tierIndex;

        [SerializeField]
        private bool useGoldCurrency;

        [SerializeField]
        private Text currencyText;

        [SerializeField]
        private Button[] pullButtons;

        [SerializeField]
        private int[] pullCounts;

        [SerializeField]
        private Button debugGrantButton;

        [SerializeField]
        private GachaResultRevealController resultReveal;

        private int _goldCostPerPull;

        private void Awake()
        {
            for (int i = 0; i < pullButtons.Length; i++)
            {
                int count = pullCounts[i];
                pullButtons[i].onClick.AddListener(() => OnPullClicked(count));
            }

            if (debugGrantButton != null)
            {
                debugGrantButton.onClick.AddListener(OnDebugGrantClicked);
            }
        }

        private void OnEnable()
        {
            if (useGoldCurrency)
            {
                GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnGoldChanged);

                _goldCostPerPull = ResolveGoldCostPerPull();

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currency))
                {
                    SetCurrencyText($"골드: {KoreanNumberFormatter.Format(currency.CurrentGold)} (1회 {KoreanNumberFormatter.Format(_goldCostPerPull)}골드)");
                }

                return;
            }

            GameBootstrapper.Events?.Subscribe<SoldierTicketChangedEvent>(OnTicketChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierTicketService tickets))
            {
                SetCurrencyText($"소환권: {tickets.CurrentTickets}");
            }
        }

        // 정식 서비스 단계에서는 티어마다 골드 비용이 서로 달라질 예정이라, 화면에 1회 뽑기당
        // 비용을 직접 표시해달라는 요청(EquipmentGachaTierPanelUI와 동일한 이유). 소환권 탭
        // (useGoldCurrency=false)에는 표시하지 않는다 - 요청 범위가 "골드 뽑기"였고, 소환권은
        // 1회 뽑기=소환권 1개로 이미 자명하다.
        private int ResolveGoldCostPerPull()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out GachaService gacha))
            {
                return 0;
            }

            GachaTableSO[] tiers = gacha.Tiers;
            return tierIndex >= 0 && tierIndex < tiers.Length ? tiers[tierIndex].GoldCostPerPull : 0;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            GameBootstrapper.Events?.Unsubscribe<SoldierTicketChangedEvent>(OnTicketChanged);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            SetCurrencyText($"골드: {KoreanNumberFormatter.Format(evt.CurrentGold)} (1회 {KoreanNumberFormatter.Format(_goldCostPerPull)}골드)");
        }

        private void OnTicketChanged(SoldierTicketChangedEvent evt)
        {
            SetCurrencyText($"소환권: {evt.CurrentTickets}");
        }

        private void OnPullClicked(int count)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out GachaService gacha))
            {
                return;
            }

            IReadOnlyList<OwnedSoldier> results = gacha.Pull(tierIndex, count);
            resultReveal.Reveal(BuildVisuals(results));
        }

        private static List<GachaResultVisual> BuildVisuals(IReadOnlyList<OwnedSoldier> results)
        {
            var visuals = new List<GachaResultVisual>(results.Count);

            foreach (OwnedSoldier owned in results)
            {
                Color border = owned.Definition.Grade != null ? owned.Definition.Grade.TintColor : NeutralBorderColor;
                visuals.Add(new GachaResultVisual(owned.Definition.Icon, border));
            }

            return visuals;
        }

        private void OnDebugGrantClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierTicketService tickets))
            {
                tickets.AddTickets(1);
            }
        }

        private void SetCurrencyText(string text)
        {
            currencyText.text = text;
        }
    }
}
