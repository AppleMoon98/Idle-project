using Core;
using Gacha;
using Gacha.Events;
using Loot;
using Loot.Events;
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
    /// 편의를 위해 디버그 지급 버튼도 함께 둔다(골드 탭은 필요 없음).
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

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currency))
                {
                    SetCurrencyText($"골드: {KoreanNumberFormatter.Format(currency.CurrentGold)}");
                }

                return;
            }

            GameBootstrapper.Events?.Subscribe<SkillScrollChangedEvent>(OnScrollChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                SetCurrencyText($"주문서: {scrolls.CurrentScrolls}");
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            GameBootstrapper.Events?.Unsubscribe<SkillScrollChangedEvent>(OnScrollChanged);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            SetCurrencyText($"골드: {KoreanNumberFormatter.Format(evt.CurrentGold)}");
        }

        private void OnScrollChanged(SkillScrollChangedEvent evt)
        {
            SetCurrencyText($"주문서: {evt.CurrentScrolls}");
        }

        private void OnPullClicked(int count)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillGachaService gacha))
            {
                gacha.Pull(tierIndex, count);
            }
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
