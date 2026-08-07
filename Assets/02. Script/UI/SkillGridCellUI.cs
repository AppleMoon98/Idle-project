using System;
using Skill;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SkillGridUI의 칸 하나. 아이콘/레벨 뱃지 어느 쪽을 탭해도 같은 동작(상세 팝업 열기)을
    /// 한다 - 장착은 더 이상 그리드에서 바로 이뤄지지 않고, 상세 팝업의 장착 버튼으로 옮겨갔다
    /// (SkillDetailPopupUI 참고).
    /// </summary>
    public sealed class SkillGridCellUI : MonoBehaviour
    {
        [SerializeField]
        private Button iconButton;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private Button levelBadgeButton;

        [SerializeField]
        private Text levelBadgeText;

        [SerializeField]
        private Color lockedIconColor = new(0.4f, 0.4f, 0.4f, 1f);

        /// <summary>
        /// 이 칸이 보여줄 스킬 하나를 초기화한다. 인스턴스화 직후 한 번 호출한다. count는 아직
        /// 레벨업 재료로 쓰이지 않은 보유 개수(뽑기/던전으로 얻은 중복분)다.
        /// </summary>
        public void Initialize(SkillSO definition, int level, int count, Action onTapped)
        {
            icon.sprite = definition.Icon;
            icon.color = level >= 1 ? definition.IconTint : lockedIconColor;
            levelBadgeText.text = count > 0 ? $"Lv.{level} ×{count}" : $"Lv.{level}";

            iconButton.onClick.AddListener(() => onTapped());
            levelBadgeButton.onClick.AddListener(() => onTapped());
        }
    }
}
