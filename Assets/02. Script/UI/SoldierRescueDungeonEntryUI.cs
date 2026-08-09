using Core;
using Dungeon;
using Stage;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 구출 던전 팝업의 "입장" 버튼을 눌렀을 때, 선택된 단계를 읽어
    /// SoldierRescueDungeonSessionController를 시작시키고 열려 있던 팝업들을 닫아 게임 화면을
    /// 드러낸다. StoneDungeonEntryUI와 동일한 형태.
    /// </summary>
    public sealed class SoldierRescueDungeonEntryUI : MonoBehaviour
    {
        [SerializeField]
        private Button enterButton;

        [SerializeField]
        private StageStepperUI stepper;

        [SerializeField]
        private SoldierRescueDungeonSessionController session;

        [SerializeField]
        private SimplePopupUI[] popupsToClose;

        private void Awake()
        {
            enterButton.onClick.AddListener(OnEnterClicked);
        }

        private void OnEnable()
        {
            if (session != null)
            {
                stepper.SetMaxStage(session.MaxStageNumber);
            }
        }

        private void OnEnterClicked()
        {
            if (!session.IsStageUnlocked(stepper.CurrentStage, out StageSO requiredStage))
            {
                string message = $"스테이지 {requiredStage.Chapter}-{requiredStage.StageNumber} 클리어 시 입장이 가능합니다.";
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent(message));
                return;
            }

            foreach (SimplePopupUI popup in popupsToClose)
            {
                popup.Close();
            }

            session.Enter(stepper.CurrentStage);
        }
    }
}
