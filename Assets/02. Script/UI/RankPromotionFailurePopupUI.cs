using Core;
using Rank;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 랭크 승급전 도중 플레이어가 죽으면 뜨는 "승급 실패" 팝업. 재도전/나가기 버튼을
    /// RankPromotionBattleController에 직접 연결한다. UI.StoneDungeonFailurePopupUI와 동일한 구조.
    /// </summary>
    public sealed class RankPromotionFailurePopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private RankPromotionBattleController battleController;

        private void Awake()
        {
            popupRoot.SetActive(false);
            retryButton.onClick.AddListener(() => battleController.Retry());
            exitButton.onClick.AddListener(() => battleController.ExitToOriginalStage());
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankPromotionAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
        }

        private void OnAttemptFailed(RankPromotionAttemptFailedEvent evt)
        {
            popupRoot.SetActive(true);
        }

        private void OnAttemptStarted(RankPromotionAttemptStartedEvent evt)
        {
            popupRoot.SetActive(false);
        }

        private void OnSessionEnded(RankPromotionSessionEndedEvent evt)
        {
            popupRoot.SetActive(false);
        }
    }
}
