using Core;
using Enhancement;
using SoldierEnhancement;
using SoldierEnhancement.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 강화 능력치 하나(행)의 표시/입력을 담당한다. UI.StatRowUI(플레이어용)와 동일한 구조의
    /// 병렬 컴포넌트다 — SoldierStatPanelUI가 SoldierEnhancementService.StatTypes 개수만큼
    /// 이 컴포넌트가 붙은 프리팹을 Instantiate하고 Initialize로 담당 스탯을 지정한다.
    /// </summary>
    public sealed class SoldierStatRowUI : MonoBehaviour
    {
        /// <summary>
        /// 강화 버튼 배수. MultiplierButtons 배열과 순서가 대응한다 (x1, x5, x10, x100, MAX).
        /// </summary>
        private static readonly int[] Multipliers = { 1, 5, 10, 100, int.MaxValue };

        [SerializeField]
        private Text infoText;

        [SerializeField]
        private Button[] multiplierButtons;

        private EnhancementStatType _statType;

        /// <summary>
        /// 이 행이 담당할 능력치를 지정하고 버튼 클릭을 연결한다. Instantiate 직후 한 번 호출한다.
        /// </summary>
        public void Initialize(EnhancementStatType statType)
        {
            _statType = statType;

            if (multiplierButtons.Length != Multipliers.Length)
            {
                Debug.LogWarning($"{nameof(SoldierStatRowUI)}: multiplierButtons({multiplierButtons.Length})와 Multipliers({Multipliers.Length}) 배열 길이가 다릅니다. 인스펙터의 버튼 배열을 확인하세요.");
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
            GameBootstrapper.Events?.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
        }

        private void OnSoldierStatEnhanced(SoldierStatEnhancedEvent evt)
        {
            if (evt.StatType == _statType)
            {
                Refresh();
            }
        }

        private void Enhance(int count)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierEnhancementService service))
            {
                service.TryEnhanceMultiple(_statType, count);
            }
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierEnhancementService service))
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
        }
    }
}
