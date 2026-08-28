using Core;
using Stage.Events;
using UnityEngine;
using UnityEngine.UI;
using War.Events;

namespace UI
{
    /// <summary>
    /// 현재 스테이지 번호와 클리어까지 남은 몬스터 수를 텍스트로 표시한다.
    /// StageChangedEvent/StageProgressChangedEvent 구독만으로 갱신되며 Stage 도메인을 직접 참조하지 않는다.
    /// 다만 War 클라이맥스에서 전멸이 아닌 목표(구조물 점령/보스 처치/수하물 보호)가 활성화되면
    /// 실제 클리어 조건과 무관한 "남은 몬스터" 수치가 오해를 줄 수 있어 그 줄만 숨긴다.
    /// 돌파/반복 표시는 더 이상 여기서 하지 않는다 — StageModeToggleUI가 "현재 모드 표시 + 전환"을
    /// 겸하는 인터랙티브 버튼으로 그 역할을 대신한다(같은 정보를 두 곳에서 따로 동기화할 필요가 없도록).
    /// 반복 대상 스테이지 선택 팝업(StageRepeatPickerPopupUI)도 더 이상 이 텍스트를 탭해서 여는 게
    /// 아니라, StageModeToggleUI에서 돌파→반복으로 전환하는 바로 그 순간 자동으로 연다.
    ///
    /// 골드 던전 등 StageController.PauseForOverlay(overlayLabel)로 시작되는 오버레이가 켜져 있는
    /// 동안은 StageOverlayLabelChangedEvent로 받은 라벨(예: "골드 던전 1층")을 그 자리에 대신
    /// 보여준다 - 오버레이 중엔 실제 진행 중인 스테이지 정보가 의미가 없기 때문. 라벨이 null이면
    /// (ResumeAfterOverlay) 평소 스테이지 표시로 되돌아간다.
    /// </summary>
    public sealed class StageInfoUI : MonoBehaviour
    {
        [SerializeField]
        private Text stageInfoText;

        private int _chapter;
        private int _stageNumber;
        private int _remainingCount;
        private int _totalCount;
        private bool _hideMonsterCount;
        private string _overlayLabel;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Subscribe<StageProgressChangedEvent>(OnStageProgressChanged);
            GameBootstrapper.Events?.Subscribe<WarClimaxWarmupStartedEvent>(OnWarClimaxWarmupStarted);
            GameBootstrapper.Events?.Subscribe<WarClimaxStateChangedEvent>(OnWarClimaxStateChanged);
            GameBootstrapper.Events?.Subscribe<StageOverlayLabelChangedEvent>(OnStageOverlayLabelChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Unsubscribe<StageProgressChangedEvent>(OnStageProgressChanged);
            GameBootstrapper.Events?.Unsubscribe<WarClimaxWarmupStartedEvent>(OnWarClimaxWarmupStarted);
            GameBootstrapper.Events?.Unsubscribe<WarClimaxStateChangedEvent>(OnWarClimaxStateChanged);
            GameBootstrapper.Events?.Unsubscribe<StageOverlayLabelChangedEvent>(OnStageOverlayLabelChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _chapter = evt.Chapter;
            _stageNumber = evt.StageNumber;
            Refresh();
        }

        private void OnStageProgressChanged(StageProgressChangedEvent evt)
        {
            _remainingCount = evt.RemainingCount;
            _totalCount = evt.TotalCount;
            Refresh();
        }

        private void OnWarClimaxWarmupStarted(WarClimaxWarmupStartedEvent evt)
        {
            // 전멸(Annihilation) 목표 삭제 이후, 이 이벤트는 항상 실제 목표(구조물 점령/수하물
            // 보호)가 배정된 클라이맥스에서만 발행된다 - 그 두 목표는 몬스터 수가 클리어 조건과
            // 무관하므로 항상 숨긴다.
            _hideMonsterCount = true;
            Refresh();
        }

        private void OnWarClimaxStateChanged(WarClimaxStateChangedEvent evt)
        {
            _hideMonsterCount = evt.IsClimax;
            Refresh();
        }

        private void OnStageOverlayLabelChanged(StageOverlayLabelChangedEvent evt)
        {
            _overlayLabel = evt.Label;
            Refresh();
        }

        private void Refresh()
        {
            if (_overlayLabel != null)
            {
                stageInfoText.text = _overlayLabel;
                return;
            }

            stageInfoText.text = _hideMonsterCount
                ? $"스테이지 {_chapter}-{_stageNumber}"
                : $"스테이지 {_chapter}-{_stageNumber}\n남은 몬스터: {_remainingCount}/{_totalCount}";
        }
    }
}
