using Core;
using Dungeon;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 보스 던전 토벌 실패 시 뜨는 "토벌 실패" 팝업. 재도전/나가기 버튼을
    /// BossDungeonSessionController에 직접 연결한다. UI.StoneDungeonFailurePopupUI와 동일한 형태.
    /// </summary>
    public sealed class BossDungeonFailurePopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private BossDungeonSessionController session;

        private void Awake()
        {
            popupRoot.SetActive(false);
            retryButton.onClick.AddListener(() => session.Retry());
            exitButton.onClick.AddListener(() => session.ExitToOriginalStage());
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<BossDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<BossDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<BossDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<BossDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<BossDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<BossDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnAttemptFailed(BossDungeonAttemptFailedEvent evt)
        {
            popupRoot.SetActive(true);
        }

        private void OnAttemptStarted(BossDungeonAttemptStartedEvent evt)
        {
            popupRoot.SetActive(false);
        }

        private void OnSessionEnded(BossDungeonSessionEndedEvent evt)
        {
            popupRoot.SetActive(false);
        }
    }
}
