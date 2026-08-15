using System.Collections.Generic;
using Core;
using Gacha;
using Gacha.Events;
using Loot;
using Loot.Events;
using Skill;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "스킬 뽑기" 카테고리 안, 티어 하나(골드/티켓/픽업 등)의 패널.
    /// SoldierGachaTierPanelUI와 동일한 형태 — 이 패널이 몇 번째 티어를 대표하는지는 tierIndex로
    /// 지정하고, SkillGachaService.Tiers[tierIndex]에서 비용/표시 이름을 그대로 읽어온다.
    /// pullButtons[i]는 pullCounts[i]개를 한 번에 뽑는 버튼이다. useGoldCurrency로 이 패널
    /// 인스턴스가 골드/주문서 중 어느 재화를 표시할지 정한다(같은 클래스를 골드 탭/티켓 탭
    /// 인스턴스 둘 다에 재사용). 주문서를 얻을 정식 경로(스킬 던전)가 있지만, 초기 테스트
    /// 편의를 위해 디버그 지급 버튼도 함께 둔다(골드 탭은 필요 없음). 뽑기 결과는 팝업
    /// (SkillPulledPopup, 이제 미사용) 대신 resultReveal이 이 패널 안에서 슬롯으로 하나씩
    /// 보여준다 - 스킬은 등급→색 매핑이 없어(SkillSO.Grade는 분류용 enum일 뿐) 테두리 색은
    /// SkillSO.IconTint(스킬별 수작업 지정 색)를 그대로 쓴다.
    /// </summary>
    public sealed class SkillGachaTierPanelUI : MonoBehaviour
    {
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

            GameBootstrapper.Events?.Subscribe<SkillScrollChangedEvent>(OnScrollChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                SetCurrencyText($"주문서: {scrolls.CurrentScrolls}");
            }
        }

        // 골드 뽑기 비용은 누적 뽑기 횟수에 따라 오를 수 있어(SkillGachaTableSO.CostIncrementTiers),
        // 화면에 매번 "다음 1회" 비용을 새로 조회해 표시한다. 주문서 탭(useGoldCurrency=false)에는
        // 표시하지 않는다 - 요청 범위가 "골드 뽑기"였고, 주문서는 1회 뽑기=주문서 1개로 이미 자명하다.
        private int ResolveGoldCostPerPull()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillGachaService gacha))
            {
                return 0;
            }

            SkillGachaTableSO[] tiers = gacha.Tiers;
            return tierIndex >= 0 && tierIndex < tiers.Length
                ? tiers[tierIndex].GetGoldCostForPull(gacha.GetGoldPullCount(tierIndex))
                : 0;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            GameBootstrapper.Events?.Unsubscribe<SkillScrollChangedEvent>(OnScrollChanged);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            _goldCostPerPull = ResolveGoldCostPerPull();
            SetCurrencyText($"골드: {KoreanNumberFormatter.Format(evt.CurrentGold)} (1회 {KoreanNumberFormatter.Format(_goldCostPerPull)}골드)");
        }

        private void OnScrollChanged(SkillScrollChangedEvent evt)
        {
            SetCurrencyText($"주문서: {evt.CurrentScrolls}");
        }

        private void OnPullClicked(int count)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillGachaService gacha))
            {
                return;
            }

            IReadOnlyList<SkillSO> results = gacha.Pull(tierIndex, count);
            resultReveal.Reveal(BuildVisuals(results));
        }

        private static List<GachaResultVisual> BuildVisuals(IReadOnlyList<SkillSO> results)
        {
            var visuals = new List<GachaResultVisual>(results.Count);

            foreach (SkillSO skill in results)
            {
                visuals.Add(new GachaResultVisual(skill.Icon, skill.IconTint));
            }

            return visuals;
        }

        private void OnDebugGrantClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                scrolls.AddScrolls(1);
            }
        }

        private void SetCurrencyText(string text)
        {
            currencyText.text = text;
        }
    }
}
