using System;
using Core;
using Enhancement;
using Enhancement.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 능력치별 강화 내용/진행도/비용을 표시하고, x1/x5/x10/x100/MAX 버튼 클릭을
    /// EnhancementService.TryEnhanceMultiple로 전달한다.
    /// </summary>
    public sealed class StatPanelUI : MonoBehaviour
    {
        /// <summary>
        /// 강화 버튼 배수. MultiplierButtons 배열과 순서가 대응한다 (x1, x5, x10, x100, MAX).
        /// </summary>
        private static readonly int[] Multipliers = { 1, 5, 10, 100, int.MaxValue };

        [Serializable]
        private sealed class StatRow
        {
            public EnhancementStatType StatType;
            public Text InfoText;
            public Button[] MultiplierButtons;
        }

        [SerializeField]
        private StatRow[] rows;

        private void Awake()
        {
            foreach (StatRow row in rows)
            {
                for (int i = 0; i < row.MultiplierButtons.Length; i++)
                {
                    EnhancementStatType statType = row.StatType;
                    int count = Multipliers[i];
                    row.MultiplierButtons[i].onClick.AddListener(() => Enhance(statType, count));
                }
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StatEnhancedEvent>(OnStatEnhanced);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StatEnhancedEvent>(OnStatEnhanced);
        }

        private void OnStatEnhanced(StatEnhancedEvent evt)
        {
            Refresh();
        }

        private void Enhance(EnhancementStatType statType, int count)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                service.TryEnhanceMultiple(statType, count);
            }
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                return;
            }

            foreach (StatRow row in rows)
            {
                RefreshRow(row, service);
            }
        }

        private static void RefreshRow(StatRow row, EnhancementService service)
        {
            int level = service.GetLevel(row.StatType);
            int maxLevel = service.GetMaxLevel(row.StatType);
            float valuePerLevel = service.GetValuePerLevel(row.StatType);
            int cost = service.GetNextCost(row.StatType);

            string costPart = cost < 0 ? "MAX" : $"{cost} G";
            row.InfoText.text = $"{DisplayName(row.StatType)} (+{valuePerLevel}/Lv)  Lv.{level}/{maxLevel}  {costPart}";
        }

        private static string DisplayName(EnhancementStatType statType)
        {
            return statType switch
            {
                EnhancementStatType.AttackPower => "공격력",
                EnhancementStatType.MaxHealth => "최대 체력",
                _ => statType.ToString()
            };
        }
    }
}
