using Core;
using Dungeon;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 던전 보스 토벌 실패 시 뜨는 "토벌 실패" 팝업. 재도전/나가기 버튼을
    /// SkillDungeonSessionController에 직접 연결한다. StoneDungeonFailurePopupUI와 동일한 형태.
    /// </summary>
    public sealed class SkillDungeonFailurePopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        private SkillDungeonSessionController session;

        private void Awake()
        {
            popupRoot.SetActive(false);
            retryButton.onClick.AddListener(() => session.Retry());
            exitButton.onClick.AddListener(() => session.ExitToOriginalStage());
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<SkillDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<SkillDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonSessionEndedEvent>(OnSessionEnded);
        }

        private void OnAttemptFailed(SkillDungeonAttemptFailedEvent evt)
        {
            popupRoot.SetActive(true);
        }

        private void OnAttemptStarted(SkillDungeonAttemptStartedEvent evt)
        {
            popupRoot.SetActive(false);
        }

        private void OnSessionEnded(SkillDungeonSessionEndedEvent evt)
        {
            popupRoot.SetActive(false);
        }
    }
}
