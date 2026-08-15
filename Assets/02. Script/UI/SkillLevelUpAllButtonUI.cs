using Core;
using Skill;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SkillPanel의 SkillSlotBar와 SubTabBar 사이에 위치한 "전체 레벨업" 버튼. 확인 팝업을 거친 뒤
    /// Skill.SkillService.TryLevelUpAll로 카탈로그의 모든 스킬을 조건이 허용하는 한 최대한
    /// 레벨업시킨다 — EquipmentSlotPopupUI의 전체 강화/합성과 동일한 확인 절차
    /// (UI.ConfirmationPopupUI 재사용, actionKey는 UI.NotificationSettingsPopupUI의 알림 설정
    /// 목록에도 추가돼 있다).
    /// </summary>
    public sealed class SkillLevelUpAllButtonUI : MonoBehaviour
    {
        private const string ActionKey = "SkillLevelUpAll";

        [SerializeField]
        private Button levelUpAllButton;

        [SerializeField]
        private SkillCatalogSO skillCatalog;

        [SerializeField]
        private ConfirmationPopupUI confirmationPopup;

        private void Awake()
        {
            levelUpAllButton.onClick.AddListener(LevelUpAll);
        }

        private void LevelUpAll()
        {
            const string message = "레벨업 조건을 충족한 스킬을 전부 레벨업합니다. 정말로 진행하시겠습니까?";

            if (confirmationPopup != null)
            {
                confirmationPopup.RequestConfirm(ActionKey, message, ExecuteLevelUpAll);
            }
            else
            {
                ExecuteLevelUpAll();
            }
        }

        private void ExecuteLevelUpAll()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService skillService))
            {
                skillService.TryLevelUpAll(skillCatalog);
            }
        }
    }
}
