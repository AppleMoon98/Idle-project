using Core;
using Gacha;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "스킬 뽑기" 카테고리 안, 티어 하나(일반 등)의 패널. SoldierGachaTierPanelUI와
    /// 동일한 형태 — 이 패널이 몇 번째 티어를 대표하는지는 tierIndex로 지정하고,
    /// SkillGachaService.Tiers[tierIndex]에서 비용/표시 이름을 그대로 읽어온다. pullButtons[i]는
    /// pullCounts[i]개를 한 번에 뽑는 버튼이다. 주문서를 얻을 정식 경로(스킬 던전)가 있지만,
    /// 초기 테스트 편의를 위해 디버그 지급 버튼도 함께 둔다.
    /// </summary>
    public sealed class SkillGachaTierPanelUI : MonoBehaviour
    {
        [SerializeField]
        private int tierIndex;

        [SerializeField]
        private Text ticketText;

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

            debugGrantButton.onClick.AddListener(OnDebugGrantClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillScrollChangedEvent>(OnScrollChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillScrollService scrolls))
            {
                SetTicketText(scrolls.CurrentScrolls);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillScrollChangedEvent>(OnScrollChanged);
        }

        private void OnScrollChanged(SkillScrollChangedEvent evt)
        {
            SetTicketText(evt.CurrentScrolls);
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

        private void SetTicketText(int amount)
        {
            ticketText.text = $"주문서: {amount}";
        }
    }
}
