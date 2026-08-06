using Dungeon;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 던전 팝업의 "입장" 버튼을 눌렀을 때, 선택된 단계를 읽어
    /// SkillDungeonSessionController를 시작시키고 열려 있던 팝업들을 닫아 게임 화면을 드러낸다.
    /// StoneDungeonEntryUI와 동일한 형태.
    /// </summary>
    public sealed class SkillDungeonEntryUI : MonoBehaviour
    {
        [SerializeField]
        private Button enterButton;

        [SerializeField]
        private StageStepperUI stepper;

        [SerializeField]
        private SkillDungeonSessionController session;

        [SerializeField]
        private SimplePopupUI[] popupsToClose;

        private void Awake()
        {
            enterButton.onClick.AddListener(OnEnterClicked);
        }

        private void OnEnterClicked()
        {
            foreach (SimplePopupUI popup in popupsToClose)
            {
                popup.Close();
            }

            session.Enter(stepper.CurrentStage);
        }
    }
}
