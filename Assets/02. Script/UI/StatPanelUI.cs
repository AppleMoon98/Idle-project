using Core;
using Enhancement;
using Enhancement.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 능력치별 현재 레벨/다음 강화 비용을 표시하고, 강화 버튼 클릭을 EnhancementService로 전달한다.
    /// </summary>
    public sealed class StatPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Text attackPowerText;

        [SerializeField]
        private Button attackPowerButton;

        [SerializeField]
        private Text maxHealthText;

        [SerializeField]
        private Button maxHealthButton;

        private void Awake()
        {
            attackPowerButton.onClick.AddListener(() => Enhance(EnhancementStatType.AttackPower));
            maxHealthButton.onClick.AddListener(() => Enhance(EnhancementStatType.MaxHealth));
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

        private void Enhance(EnhancementStatType statType)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                service.TryEnhance(statType);
            }
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                return;
            }

            SetStatText(attackPowerText, EnhancementStatType.AttackPower, service);
            SetStatText(maxHealthText, EnhancementStatType.MaxHealth, service);
        }

        private static void SetStatText(Text text, EnhancementStatType statType, EnhancementService service)
        {
            int level = service.GetLevel(statType);
            int cost = service.GetNextCost(statType);

            text.text = cost < 0
                ? $"{statType} Lv.{level} (MAX)"
                : $"{statType} Lv.{level} - Cost: {cost}";
        }
    }
}
