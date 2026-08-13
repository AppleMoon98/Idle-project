using Core;
using Rank;
using Rank.Events;
using Stage;
using Stage.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 어디서나 접근 가능한 돌파/반복 스테이지 진행 방침 토글 버튼. 랭크 승급이 가능해지면
    /// (RankService.IsNextRankAvailable) 버튼 표기가 "승급"으로 바뀌고, 그 상태에서 누르면 돌파/반복
    /// 전환 대신 RankPromotionBattleController로 승급전을 시작한다. 승급전이 진행 중인 동안은
    /// (RankPromotionAttemptStartedEvent~RankPromotionSessionEndedEvent) "승급" 표기를 잠깐 접어두고
    /// 평소의 돌파/반복 표기로 되돌아간다 - RankUpAvailableTextUI가 이 구간에 스스로 숨는 것과 같은
    /// 이유(중복 시작 방지).
    /// </summary>
    public sealed class StageModeToggleUI : MonoBehaviour
    {
        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Text modeLabelText;

        [SerializeField]
        private RankPromotionBattleController battleController;

        private bool _isPromotionBattleActive;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageModeChangedEvent>(OnStageModeChanged);
            GameBootstrapper.Events?.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            GameBootstrapper.Events?.Subscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
            toggleButton.onClick.AddListener(OnToggleClicked);

            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageModeChangedEvent>(OnStageModeChanged);
            GameBootstrapper.Events?.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
            toggleButton.onClick.RemoveListener(OnToggleClicked);
        }

        private void OnToggleClicked()
        {
            if (IsPromotionAvailable(out RankService rankService))
            {
                battleController?.Enter(rankService.GetNextRank());
                return;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                modeService.Toggle();
            }
        }

        private void OnStageModeChanged(StageModeChangedEvent evt)
        {
            Refresh();
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
            _isPromotionBattleActive = true;
            Refresh();
        }

        private void OnSessionEnded(RankPromotionSessionEndedEvent evt)
        {
            _isPromotionBattleActive = false;
            Refresh();
        }

        private bool IsPromotionAvailable(out RankService rankService)
        {
            rankService = null;

            if (_isPromotionBattleActive || GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out rankService))
            {
                return false;
            }

            return rankService.IsNextRankAvailable();
        }

        private void Refresh()
        {
            if (IsPromotionAvailable(out _))
            {
                modeLabelText.text = "승급";
                return;
            }

            StageProgressionMode mode = StageProgressionMode.Breakthrough;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                mode = modeService.CurrentMode;
            }

            modeLabelText.text = mode == StageProgressionMode.Repeat ? "반복" : "돌파";
        }
    }
}
