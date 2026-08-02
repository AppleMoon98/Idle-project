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
    /// </summary>
    public sealed class EquipmentGachaTierPanelUI : MonoBehaviour
    {
        [SerializeField]
        private int tierIndex;

        [SerializeField]
        private Text goldText;

        [SerializeField]
        private Button pullButton;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnGoldChanged);
            pullButton.onClick.AddListener(OnPullClicked);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currency))
            {
                SetGoldText(currency.CurrentGold);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            pullButton.onClick.RemoveListener(OnPullClicked);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            SetGoldText(evt.CurrentGold);
        }

        private void OnPullClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentGachaService gacha))
            {
                gacha.TryPull(tierIndex, out _);
            }
        }

        private void SetGoldText(int amount)
        {
            goldText.text = $"Gold: {amount}";
        }
    }
}
