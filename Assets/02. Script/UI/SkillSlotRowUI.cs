using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 하나(행)의 표시/레벨업 입력을 담당한다. SkillPanelUI가 SkillCatalogSO 개수만큼
    /// 이 컴포넌트가 붙은 프리팹을 Instantiate하고 Initialize로 담당 스킬을 지정한다.
    /// </summary>
    public sealed class SkillSlotRowUI : MonoBehaviour
    {
        [SerializeField]
        private Text infoText;

        [SerializeField]
        private Button levelUpButton;

        private SkillSO _definition;

        public void Initialize(SkillSO definition)
        {
            _definition = definition;
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
            Refresh();
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            if (evt.Definition == _definition)
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
            if (_definition == null || GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillService service))
            {
                return;
            }

            int level = service.GetLevel(_definition);
            bool isMax = level >= _definition.MaxLevel;
            float magnitude = _definition.GetMagnitude(level);

            string costPart = isMax
                ? "MAX"
                : $"{_definition.GetGoldCost(level)} G / {_definition.GetStoneCost(level)} 강화석";

            infoText.text = $"{_definition.DisplayName}  Lv.{level}/{_definition.MaxLevel}  (수치 {magnitude:F0})  {costPart}";
            levelUpButton.interactable = !isMax;
        }
    }
}
