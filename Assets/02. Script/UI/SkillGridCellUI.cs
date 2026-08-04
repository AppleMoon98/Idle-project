using System;
using Skill;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SkillGridUI의 칸 하나. 아이콘을 탭하면 선택된 슬롯에 장착을, 레벨 뱃지를 탭하면
    /// 레벨업 팝업을 연다 — Equipment.EquipmentRowUI가 이름 버튼과 장착 버튼을 분리해둔 것과
    /// 같은 이유로 두 동작의 탭 영역을 분리했다.
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
        /// 이 칸이 보여줄 스킬 하나를 초기화한다. 인스턴스화 직후 한 번 호출한다.
        /// </summary>
        public void Initialize(SkillSO definition, int level, Action onEquipTapped, Action onLevelBadgeTapped)
        {
            icon.sprite = definition.Icon;
            icon.color = level >= 1 ? definition.IconTint : lockedIconColor;
            levelBadgeText.text = $"Lv.{level}";

            iconButton.onClick.AddListener(() => onEquipTapped());
            levelBadgeButton.onClick.AddListener(() => onLevelBadgeTapped());
        }
    }
}
