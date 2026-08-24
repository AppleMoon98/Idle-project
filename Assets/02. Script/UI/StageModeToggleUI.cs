using Core;
using Rank;
using Rank.Events;
using Stage;
using Stage.Events;
using UI.Events;
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
    /// 이유(중복 시작 방지). StageModeChangedEvent를 받을 때마다 토스트로 "돌파/반복 모드로
    /// 변경되었습니다."를 안내한다. 던전 등 오버레이가 활성 중(StageController.IsOverlayActive)
    /// 이면 모드 전환 자체를 막고 토스트로 안내한다 - 오버레이 중 LoadStage가 실행돼 스포너/
    /// 트래커 상태가 꼬이는 버그가 실사용 중 발견됐다(StageProgression.JumpTo 등의 _isSuppressed
    /// 체크 누락이 근본 원인이라 거기서도 막혔지만, 여기서 먼저 막아 사용자에게 이유를 알려준다).
    /// 반복 모드일 때는 themeSwitcher(UI.ButtonColorThemeSwitcherUI)로 버튼 배경을 빨간 테마로
    /// 바꿔 돌파 모드와 시각적으로 구분한다.
    /// </summary>
    public sealed class StageModeToggleUI : MonoBehaviour
    {
        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Text modeLabelText;

        [SerializeField]
        private RankPromotionBattleController battleController;

        [SerializeField]
        private StageRepeatPickerPopupUI repeatPickerPopup;

        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private ButtonColorThemeSwitcherUI themeSwitcher;

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

            if (stageController != null && stageController.IsOverlayActive)
            {
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("던전 입장 중에는 스테이지 모드를 변경할 수 없습니다."));
                return;
            }

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                return;
            }

            if (modeService.CurrentMode == StageProgressionMode.Breakthrough)
            {
                // 돌파 -> 반복 전환은 이 시점에 바로 확정하지 않는다 - 팝업에서 실제로 반복할
                // 스테이지를 골라야 비로소 모드가 반복으로 바뀐다(StageRepeatPickerPopupUI.OnPicked
                // 참고). 팝업을 닫기만 하고 아무것도 안 고르면 돌파 모드 그대로 남는다.
                repeatPickerPopup?.Open();
                return;
            }

            // 반복 -> 돌파는 선택 절차 없이 즉시 전환하되, 방금까지 반복하던 스테이지에 그대로
            // 머무르지 않고 실제 돌파 프론티어(역대 최고 기록 + 1)로 되돌아간다.
            modeService.Toggle();
            stageController?.JumpCurrentToBreakthroughFrontier();
        }

        private void OnStageModeChanged(StageModeChangedEvent evt)
        {
            Refresh();

            string modeName = evt.Mode == StageProgressionMode.Repeat ? "반복" : "돌파";
            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent($"{modeName} 모드로 변경되었습니다.", ToastMessageType.Info));
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
            StageProgressionMode mode = StageProgressionMode.Breakthrough;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                mode = modeService.CurrentMode;
            }

            themeSwitcher?.SetAlert(mode == StageProgressionMode.Repeat);

            if (IsPromotionAvailable(out _))
            {
                modeLabelText.text = "승급";
                return;
            }

            modeLabelText.text = mode == StageProgressionMode.Repeat ? "반복" : "돌파";
        }
    }
}
