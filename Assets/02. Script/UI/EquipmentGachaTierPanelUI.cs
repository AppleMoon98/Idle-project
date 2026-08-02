using Core;
using Gacha;
using Loot;
using Loot.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "무기 뽑기" 카테고리 안, 티어 하나(일반/고급/유료 등)의 패널.
    /// SoldierGachaTierPanelUI(병사 뽑기)와 대칭되는 구조 — tierIndex로 이 패널이 몇 번째
    /// 티어인지 지정하고, EquipmentGachaService.Tiers[tierIndex]에서 비용을 그대로 읽어온다.
    /// pullButtons[i]는 pullCounts[i]개를 한 번에 뽑는 버튼이다(1/10/30/300개 등).
    /// </summary>
    public sealed class EquipmentGachaTierPanelUI : MonoBehaviour
    {
        [SerializeField]
        private int tierIndex;

        [SerializeField]
        private Text goldText;

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
            GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnGoldChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currency))
            {
                SetGoldText(currency.CurrentGold);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            SetGoldText(evt.CurrentGold);
        }

        private void OnPullClicked(int count)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentGachaService gacha))
            {
                gacha.Pull(tierIndex, count);
            }
        }

        private void SetGoldText(int amount)
        {
            goldText.text = $"Gold: {amount}";
        }
    }
}
