using Dungeon;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 골드 던전 팝업의 "입장" 버튼을 눌렀을 때, 선택된 단계를 읽어 GoldDungeonSessionController를
    /// 시작시키고 열려 있던 팝업들을 닫아 게임 화면을 드러낸다. 던전 진행 로직 자체는 전혀 모른다.
    /// </summary>
    public sealed class GoldDungeonEntryUI : MonoBehaviour
    {
        [SerializeField]
        private Button enterButton;

        [SerializeField]
        private StageStepperUI stepper;

        [SerializeField]
        private GoldDungeonSessionController session;

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
