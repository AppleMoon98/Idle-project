using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// "이름 + 선택 버튼" 형태의 범용 행. 배치 피커 팝업(SoldierDeploymentPickerPopupUI)이
    /// 로스터 유닛 목록을 나열할 때 재사용한다.
    /// </summary>
    public sealed class SoldierPickerRowUI : MonoBehaviour
    {
        [SerializeField]
        private Text label;

        [SerializeField]
        private Button selectButton;

        /// <summary>
        /// 행 데이터를 채운다. onSelected는 선택 버튼을 눌렀을 때 호출되는 콜백이다.
        /// </summary>
        public void Initialize(string displayText, Action onSelected)
        {
            label.text = displayText;
            selectButton.onClick.AddListener(() => onSelected?.Invoke());
        }
    }
}
