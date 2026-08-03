using Enhancement;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 강화 팝업에서 능력치 한 종류의 "현재값 → 다음값"을 보여주는 행. 슬롯이 능력치를 여러 개
    /// 가질 수 있으므로(EquipmentStatConfigSO.GetEntries), EquipmentEnhancementPopupUI가 능력치
    /// 개수만큼 이 컴포넌트를 Instantiate한다.
    /// </summary>
    public sealed class EquipmentStatPreviewRowUI : MonoBehaviour
    {
        [SerializeField]
        private Text statText;

        public void Initialize(EnhancementStatType statType, float currentValue, float nextValue)
        {
            string name = StatDisplayNames.Get(statType);
            string current = StatDisplayNames.FormatValue(statType, currentValue);
            string next = StatDisplayNames.FormatValue(statType, nextValue);
            statText.text = $"{name}  {current} → {next}";
        }
    }
}
