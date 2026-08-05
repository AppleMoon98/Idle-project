using Core;
using Rank;
using Rank.Events;
using Stage.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// StageInfoText 바로 아래, "랭크 승급 가능" 문구를 겸하는 버튼. RankService.IsNextRankAvailable이
    /// true일 때만 보이고, 누르면 RankPromotionBattleController로 다음 랭크의 승급전을 시작한다.
    /// 승급전이 진행 중인 동안은 숨겨서 중복 시작을 막는다.
    /// </summary>
    public sealed class RankUpAvailableTextUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject root;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Button button;

        [SerializeField]
        private RankPromotionBattleController battleController;

        private void Awake()
        {
            root.SetActive(false);
            button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            GameBootstrapper.Events?.Subscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            Refresh();
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            Refresh();
        }

        private void OnAttemptStarted(RankPromotionAttemptStartedEvent evt)
        {
            root.SetActive(false);
        }

        private void OnSessionEnded(RankPromotionSessionEndedEvent evt)
        {
            Refresh();
        }

        private void OnClicked()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                return;
            }

            RankSO nextRank = rankService.GetNextRank();

            if (nextRank != null)
            {
                battleController.Enter(nextRank);
            }
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                root.SetActive(false);
                return;
            }

            bool available = rankService.IsNextRankAvailable();
            root.SetActive(available);

            if (available)
            {
                label.text = "랭크 승급 가능";
            }
        }
    }
}
