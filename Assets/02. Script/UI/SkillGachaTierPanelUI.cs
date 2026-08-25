using System.Collections.Generic;
using Core;
using Gacha;
using Gacha.Events;
using Loot;
using Loot.Events;
using Skill;
using Skill.Events;
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
    ///
    /// 재화 텍스트에 "실행 가능 횟수"를 함께 표시하고(GachaAffordabilityCalculator), 모든 스킬이
    /// 최대 레벨이라 뽑아도 소용없는 상태면 뽑기 버튼을 비활성화한다 - 요청한 횟수보다 적게
    /// 실행되고도(0회 포함) 아무 안내 없이 조용히 끝나던 문제(GitHub 이슈 #22)를, 실제 부분
    /// 성공/실패는 SkillGachaService.Pull()이 토스트로 알리고, 이 패널은 그 전에 미리 상태를
    /// 보여주는 역할을 나눠 맡는다.
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
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            RefreshMaxLevelGating();

            if (useGoldCurrency)
            {
                GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnGoldChanged);

                _goldCostPerPull = ResolveGoldCostPerPull();

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currency))
                {
                    SetCurrencyText($"골드: {KoreanNumberFormatter.Format(currency.CurrentGold)} (1회 {KoreanNumberFormatter.Format(_goldCostPerPull)}골드{FormatGoldAffordableSuffix(currency.CurrentGold)})");
                }

                return;
            }

            GameBootstrapper.Events?.Subscribe<SkillScrollChangedEvent>(OnScrollChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                SetCurrencyText($"주문서: {scrolls.CurrentScrolls}{FormatScrollAffordableSuffix(scrolls.CurrentScrolls)}");
            }
        }

        // 골드 뽑기 비용은 누적 뽑기 횟수에 따라 오를 수 있어(SkillGachaTableSO.CostIncrementTiers),
        // 화면에 매번 "다음 1회" 비용을 새로 조회해 표시한다.
        private int ResolveGoldCostPerPull()
        {
            SkillGachaTableSO table = ResolveTable(out SkillGachaService gacha);
            return table != null ? table.GetGoldCostForPull(gacha.GetGoldPullCount(tierIndex)) : 0;
        }

        // 골드 잔액으로 지금 몇 회를 연속으로 더 뽑을 수 있는지(GitHub 이슈 #22 - "버튼에 실제
        // 비용/실행 가능 횟수 표시"). 회차마다 비용이 오를 수 있어(CostIncrementTiers) 실제
        // 시뮬레이션이 필요하다(GachaAffordabilityCalculator).
        private string FormatGoldAffordableSuffix(BigNumber goldBalance)
        {
            SkillGachaTableSO table = ResolveTable(out SkillGachaService gacha);
            return table == null
                ? ""
                : FormatAffordableCountSuffix(GachaAffordabilityCalculator.CalculateMaxAffordableGoldPulls(
                    goldBalance, gacha.GetGoldPullCount(tierIndex), pulls => table.GetGoldCostForPull(pulls)));
        }

        // 주문서는 회차당 고정 비용이라 나눗셈 한 번으로 충분하다.
        private string FormatScrollAffordableSuffix(int scrollBalance)
        {
            SkillGachaTableSO table = ResolveTable(out _);
            return table == null
                ? ""
                : FormatAffordableCountSuffix(GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(scrollBalance, table.TicketCostPerPull));
        }

        private static string FormatAffordableCountSuffix(int affordable)
        {
            return affordable >= GachaAffordabilityCalculator.MaxSimulatedPulls
                ? $" / 최대 {affordable}회 이상 가능"
                : $" / 최대 {affordable}회 가능";
        }

        private SkillGachaTableSO ResolveTable(out SkillGachaService gacha)
        {
            gacha = null;

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out gacha))
            {
                return null;
            }

            SkillGachaTableSO[] tiers = gacha.Tiers;
            return tierIndex >= 0 && tierIndex < tiers.Length ? tiers[tierIndex] : null;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            GameBootstrapper.Events?.Unsubscribe<SkillScrollChangedEvent>(OnScrollChanged);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            _goldCostPerPull = ResolveGoldCostPerPull();
            SetCurrencyText($"골드: {KoreanNumberFormatter.Format(evt.CurrentGold)} (1회 {KoreanNumberFormatter.Format(_goldCostPerPull)}골드{FormatGoldAffordableSuffix(evt.CurrentGold)})");
        }

        private void OnScrollChanged(SkillScrollChangedEvent evt)
        {
            SetCurrencyText($"주문서: {evt.CurrentScrolls}{FormatScrollAffordableSuffix(evt.CurrentScrolls)}");
        }

        // 이 티어에 레벨업 가능한(만렙이 아닌) 스킬이 하나도 안 남으면 뽑아도 항상 실패만 하므로
        // (SkillGachaService.Pull이 매번 "모든 스킬이 최대 레벨입니다." 토스트만 반복해서 띄우게
        // 됨), 그 상태를 버튼 자체에서 미리 보여준다(GitHub 이슈 #22 - "최대 레벨 상태 일관된 표시").
        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            RefreshMaxLevelGating();
        }

        private void RefreshMaxLevelGating()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillGachaService gacha))
            {
                return;
            }

            bool hasAnyLevelable = gacha.HasAnyLevelableCandidate(tierIndex);

            foreach (Button button in pullButtons)
            {
                button.interactable = hasAnyLevelable;
            }
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
