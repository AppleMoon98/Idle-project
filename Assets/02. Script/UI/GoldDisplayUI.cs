using Core;
using Loot;
using Loot.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 보유 골드를 텍스트로 표시한다. 최초 진입 시 CurrencyService에서 초기값을 읽고,
    /// 이후에는 GoldChangedEvent 구독만으로 갱신한다.
    /// </summary>
    public sealed class GoldDisplayUI : MonoBehaviour
    {
        [SerializeField]
        private Text goldText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnGoldChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currencyService))
            {
                SetGoldText(currencyService.CurrentGold);
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

        private void SetGoldText(BigNumber amount)
        {
            goldText.text = $"Gold\n{KoreanNumberFormatter.Format(amount)}";
        }
    }
}
