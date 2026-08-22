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
        private Image iconImage;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Button selectButton;

        /// <summary>
        /// 행 데이터를 채운다. onSelected는 선택 버튼을 눌렀을 때 호출되는 콜백이다.
        /// icon이 null이면 아이콘 이미지를 숨긴다. iconColor(기본 흰색)는 등급 틴트 등으로
        /// 아이콘 자체에 색을 입히고 싶을 때 넘긴다. iconScale(기본 1)은 실루엣 플레이스홀더보다
        /// 여백이 많은 아이콘의 렌더링 크기를 개별적으로 보정하기 위한 것(Soldier.SoldierSO.IconScale).
        /// </summary>
        public void Initialize(string displayText, Action onSelected, Sprite icon = null, Color? iconColor = null, float iconScale = 1f)
        {
            label.text = displayText;
            selectButton.onClick.AddListener(() => onSelected?.Invoke());

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.color = iconColor ?? Color.white;
                iconImage.rectTransform.localScale = Vector3.one * iconScale;
            }
        }
    }
}
