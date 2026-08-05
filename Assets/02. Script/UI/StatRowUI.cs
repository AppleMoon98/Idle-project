using Core;
using Enhancement;
using Enhancement.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 강화 능력치 하나(행)의 표시/입력을 담당한다. StatPanelUI가 EnhancementConfigSO 개수만큼
    /// 이 컴포넌트가 붙은 프리팹을 Instantiate하고 Initialize로 담당 스탯을 지정한다.
    /// </summary>
    public sealed class StatRowUI : MonoBehaviour
    {
        /// <summary>
        /// 강화 버튼 배수. MultiplierButtons 배열과 순서가 대응한다 (x1, x5, x10, x100, MAX).
        /// </summary>
        private static readonly int[] Multipliers = { 1, 5, 10, 100, int.MaxValue };

        [SerializeField]
        private Text infoText;

        [SerializeField]
        private Button[] multiplierButtons;

        [SerializeField]
        private GameObject lockOverlay;

        [SerializeField]
        private Text lockOverlayText;

        private EnhancementStatType _statType;

        /// <summary>
        /// 이 행이 담당할 능력치를 지정하고 버튼 클릭을 연결한다. Instantiate 직후 한 번 호출한다.
        /// </summary>
        public void Initialize(EnhancementStatType statType)
        {
            _statType = statType;

            if (multiplierButtons.Length != Multipliers.Length)
            {
                Debug.LogWarning($"{nameof(StatRowUI)}: multiplierButtons({multiplierButtons.Length})와 Multipliers({Multipliers.Length}) 배열 길이가 다릅니다. 인스펙터의 버튼 배열을 확인하세요.");
            }

            int buttonCount = Mathf.Min(multiplierButtons.Length, Multipliers.Length);

            for (int i = 0; i < buttonCount; i++)
            {
                int count = Multipliers[i];
                multiplierButtons[i].onClick.AddListener(() => Enhance(count));
            }

            // OnEnable이 Instantiate 직후(이 메서드 호출 전) 이미 한 번 Refresh를 실행했을 수 있어
            // 그때는 _statType이 아직 기본값이었다. 여기서 실제 값으로 다시 갱신한다.
            Refresh();
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
            if (evt.StatType == _statType)
            {
                Refresh();
                return;
            }

            // 이 행이 다른 능력치(예: 공격력)의 레벨을 조건으로 잠겨있는 경우, 그 조건이 되는
            // 능력치가 강화됐을 때도 잠금이 풀렸는지 다시 확인해야 한다 - 그렇지 않으면 조건을
            // 만족한 뒤에도 이 행 자체를 강화(=자기 자신의 StatEnhancedEvent 발생)하기 전까지는
            // 잠금 표시가 그대로 남는다.
            if (GameBootstrapper.Services != null
                && GameBootstrapper.Services.TryGet(out EnhancementService service)
                && service.HasUnlockRequirement(_statType)
                && service.GetRequiredStatType(_statType) == evt.StatType)
            {
                Refresh();
            }
        }

        private void Enhance(int count)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                service.TryEnhanceMultiple(_statType, count);
            }
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                return;
            }

            int level = service.GetLevel(_statType);
            int maxLevel = service.GetMaxLevel(_statType);
            float valuePerLevel = service.GetValuePerLevel(_statType);
            BigNumber cost = service.GetNextCost(_statType);

            string costPart = cost < 0 ? "MAX" : $"{KoreanNumberFormatter.Format(cost)} G";
            string valuePart = StatDisplayNames.FormatValue(_statType, valuePerLevel);
            infoText.text = $"{StatDisplayNames.Get(_statType)} (+{valuePart}/Lv)  Lv.{level}/{maxLevel}  {costPart}";

            bool unlocked = service.IsUnlocked(_statType);

            foreach (Button button in multiplierButtons)
            {
                button.interactable = unlocked;
            }

            if (lockOverlay != null)
            {
                lockOverlay.SetActive(!unlocked);
            }

            if (!unlocked && lockOverlayText != null)
            {
                EnhancementStatType requiredStatType = service.GetRequiredStatType(_statType);
                int requiredLevel = service.GetRequiredLevel(_statType);
                lockOverlayText.text = $"{StatDisplayNames.Get(requiredStatType)} Lv.{requiredLevel} 필요";
            }
        }
    }
}
