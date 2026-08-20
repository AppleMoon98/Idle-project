using Dungeon;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 구출 던전 팝업의 "입장" 버튼을 눌렀을 때, 선택된 단계를 읽어
    /// SoldierRescueDungeonSessionController를 시작시키고 열려 있던 팝업들을 닫아 게임 화면을
    /// 드러낸다. 입장에는 스테이지 클리어 조건이 없다 — 단계 상한(MaxStageNumber)만 적용된다.
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
            foreach (SimplePopupUI popup in popupsToClose)
            {
                popup.Close();
            }

            session.Enter(stepper.CurrentStage);
        }
    }
}
