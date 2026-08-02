using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 하나의 상세 정보(아이콘/레벨/수치/다음 비용)와 레벨업 버튼을 보여주는 팝업.
    /// SkillSlotBarUI가 슬롯을 누르면 이 팝업을 연다. 장비와 달리 스킬은 슬롯당 항목이
    /// 하나뿐이라(교체/비교 개념 없음) EquipmentDetailPopupUI보다 단순하다.
    /// </summary>
    public sealed class SkillDetailPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private Text nameText;

        [SerializeField]
        private Text infoText;

        [SerializeField]
        private Button levelUpButton;

        [SerializeField]
        private Button closeButton;

        private SkillSO _definition;
        private bool _isOpen;

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
        }

        /// <summary>
        /// 지정된 스킬의 정보를 채워 팝업을 연다.
        /// </summary>
        public void Open(SkillSO definition)
        {
            _definition = definition;
            _isOpen = true;
            popupRoot.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// 팝업을 닫는다. SkillSlotBarUI가 자신이 비활성화될 때(스킬 탭을 닫을 때) 같이 닫기 위해
        /// 외부에서 호출할 수 있어야 한다(EquipmentSlotPopupUI.Close와 동일한 이유).
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            if (_isOpen && evt.Definition == _definition)
            {
                Refresh();
            }
        }

        private void OnLevelUpClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService service))
            {
                service.TryLevelUp(_definition);
            }
        }

        private void Refresh()
        {
            if (_definition == null
                || GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SkillService service))
            {
                return;
            }

            icon.sprite = _definition.Icon;
            icon.color = _definition.IconTint;
            nameText.text = _definition.DisplayName;

            int level = service.GetLevel(_definition);
            bool isMax = level >= _definition.MaxLevel;
            float magnitude = _definition.GetMagnitude(level);

            string costPart = isMax
                ? "MAX"
                : $"{_definition.GetGoldCost(level)} G / {_definition.GetStoneCost(level)} 강화석";

            infoText.text = $"Lv.{level}/{_definition.MaxLevel}\n수치 {magnitude:F0}\n{costPart}";
            levelUpButton.interactable = !isMax;
        }
    }
}
