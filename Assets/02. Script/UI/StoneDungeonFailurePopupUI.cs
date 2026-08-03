using Core;
using Dungeon;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 강화석 던전 보스 토벌 실패 시 뜨는 "토벌 실패" 팝업. 재도전/나가기 버튼을
    /// StoneDungeonSessionController에 직접 연결한다. 재도전으로 다음 시도가 시작되거나
    /// 세션이 완전히 종료되면 무조건 숨긴다.
    /// </summary>
    public sealed class StoneDungeonFailurePopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private StoneDungeonSessionController session;

        private void Awake()
        {
            popupRoot.SetActive(false);
            retryButton.onClick.AddListener(() => session.Retry());
            exitButton.onClick.AddListener(() => session.ExitToOriginalStage());
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StoneDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<StoneDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<StoneDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnAttemptFailed(StoneDungeonAttemptFailedEvent evt)
        {
            popupRoot.SetActive(true);
        }

        private void OnAttemptStarted(StoneDungeonAttemptStartedEvent evt)
        {
            popupRoot.SetActive(false);
        }

        private void OnSessionEnded(StoneDungeonSessionEndedEvent evt)
        {
            popupRoot.SetActive(false);
        }
    }
}
