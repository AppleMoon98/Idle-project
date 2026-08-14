using System.Collections.Generic;
using Core;
using Equipment;
using Gacha;
using Loot;
using Loot.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 가챠 팝업의 "장비 뽑기" 카테고리 안, 티어 하나(일반/고급/유료 등)의 패널.
    /// SoldierGachaTierPanelUI(병사 뽑기)와 대칭되는 구조 — tierIndex로 이 패널이 몇 번째
    /// 티어인지 지정하고, 실제로 어느 슬롯(무기/장갑/갑옷/투구/신발)을 뽑을지는 slotSelector가
    /// 들고 있는 값을 뽑기 시점에 그대로 읽어간다. pullButtons[i]는 pullCounts[i]개를 한 번에
    /// 뽑는 버튼이다(1/10/30/300개 등). 뽑기 결과는 팝업(EquipmentPulledPopup, 이제 미사용) 대신
    /// resultReveal이 이 패널 안에서 슬롯으로 하나씩 보여준다.
    /// </summary>
    public sealed class EquipmentGachaTierPanelUI : MonoBehaviour
    {
        private static readonly Color NeutralBorderColor = new(0.35f, 0.35f, 0.35f, 1f);

        [SerializeField]
        private int tierIndex;

        [SerializeField]
        private EquipmentSlotSelectorUI slotSelector;

        [SerializeField]
        private Text goldText;

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
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EquipmentGachaService gacha))
            {
                return;
            }

            IReadOnlyList<EquipmentSO> results = gacha.Pull(slotSelector.SelectedSlot, tierIndex, count);
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

        private void SetGoldText(BigNumber amount)
        {
            goldText.text = $"Gold: {KoreanNumberFormatter.Format(amount)}";
        }
    }
}
