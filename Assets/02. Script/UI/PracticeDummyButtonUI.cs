using Core;
using Stage;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 연습 스테이지(허수아비) 진입/복귀 토글 버튼. UI.PromotionTestButtonUI(랭크 승급전 디버그
    /// 진입용)를 대체한다 - 랭크와 무관한 별개 기능이라 클래스를 새로 나눴다. 비활성 상태에서
    /// 누르면 Stage.PracticeStageController.TryEnter()를 시도하고, 실패하면(이미 다른 오버레이 중,
    /// 즉 던전 안) 토스트로 안내한다. 활성 상태에서 누르면 Exit()으로 복귀한다.
    /// </summary>
    public sealed class PracticeDummyButtonUI : MonoBehaviour
    {
        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Text label;

        [SerializeField]
        private PracticeStageController practiceStageController;

        [SerializeField]
        private string enterLabel = "허수아비 테스트";

        [SerializeField]
        private string exitLabel = "돌아가기";

        private void OnEnable()
        {
            toggleButton.onClick.AddListener(OnClicked);
            Refresh();
        }

        private void OnDisable()
        {
            toggleButton.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            if (practiceStageController == null)
            {
                return;
            }

            if (practiceStageController.IsActive)
            {
                practiceStageController.Exit();
            }
            else if (!practiceStageController.TryEnter())
            {
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("던전 안에서는 연습 스테이지를 사용할 수 없습니다."));
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (label == null || practiceStageController == null)
            {
                return;
            }

            label.text = practiceStageController.IsActive ? exitLabel : enterLabel;
        }
    }
}
