using Core;
using Dungeon;
using Stage;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 골드 던전 팝업의 "입장" 버튼을 눌렀을 때, 선택된 단계를 읽어 GoldDungeonSessionController를
    /// 시작시키고 열려 있던 팝업들을 닫아 게임 화면을 드러낸다. 던전 진행 로직 자체는 전혀 모른다.
    /// 입장 전 session.IsStageUnlocked로 그 단계의 기준 스테이지를 실제로 클리어했는지 먼저 확인하고,
    /// 미충족이면 입장하지 않고 ToastMessageRequestedEvent만 발행한다(전역 토스트 UI가 표시를 담당).
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

        private void OnEnable()
        {
            // OnEnable은 같은 활성화 프레임의 모든 Awake가 끝난 뒤 호출되므로,
            // stepper.Awake()가 minStage로 초기화를 마친 뒤 안전하게 상한을 덮어쓸 수 있다.
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
