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
    /// 가챠 팝업의 장비 슬롯별 카테고리(무기 뽑기/장갑 뽑기/갑옷 뽑기/투구 뽑기/신발 뽑기) 안,
    /// 티어 하나(일반/고급/유료 등)의 패널. SoldierGachaTierPanelUI(병사 뽑기)와 대칭되는 구조 —
    /// tierIndex로 이 패널이 몇 번째 티어인지, fixedSlot으로 어느 장비 슬롯을 뽑을지 지정한다
    /// (이전에는 슬롯을 카테고리 안에서 드롭다운으로 고르게 했으나, 슬롯 자체가 최상위 카테고리
    /// 탭으로 승격되면서 패널마다 고정 슬롯 하나만 담당한다). pullButtons[i]는 pullCounts[i]개를
    /// 한 번에 뽑는 버튼이다(1/10/30/300개 등). 뽑기 결과는 팝업(EquipmentPulledPopup, 이제
    /// 미사용) 대신 resultReveal이 이 패널 안에서 슬롯으로 하나씩 보여준다.
    /// </summary>
    public sealed class EquipmentGachaTierPanelUI : MonoBehaviour
    {
        private static readonly Color NeutralBorderColor = new(0.35f, 0.35f, 0.35f, 1f);

        [SerializeField]
        private int tierIndex;

        [SerializeField]
        private EquipmentType fixedSlot;

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

            IReadOnlyList<EquipmentSO> results = gacha.Pull(fixedSlot, tierIndex, count);
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
