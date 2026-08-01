using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// "프로필 이름 + 선택 버튼" 형태의 행. SoldierBehaviorProfilePopupUI가 카탈로그의
    /// 프로필 목록(+ 해제 옵션)을 나열할 때 사용한다.
    /// </summary>
    public sealed class SoldierBehaviorProfileRowUI : MonoBehaviour
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
