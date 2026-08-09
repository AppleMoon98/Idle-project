using Core;
using Dungeon;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 구출 던전 시도 실패(제한시간 초과/플레이어 사망) 시 뜨는 "구출 실패" 팝업.
    /// StoneDungeonFailurePopupUI와 동일한 형태.
    /// </summary>
    public sealed class SoldierRescueDungeonFailurePopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private SoldierRescueDungeonSessionController session;

        private void Awake()
        {
            popupRoot.SetActive(false);
            retryButton.onClick.AddListener(() => session.Retry());
            exitButton.onClick.AddListener(() => session.ExitToOriginalStage());
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnAttemptFailed(SoldierRescueDungeonAttemptFailedEvent evt)
        {
            popupRoot.SetActive(true);
        }

        private void OnAttemptStarted(SoldierRescueDungeonAttemptStartedEvent evt)
        {
            popupRoot.SetActive(false);
        }

        private void OnSessionEnded(SoldierRescueDungeonSessionEndedEvent evt)
        {
            popupRoot.SetActive(false);
        }
    }
}
