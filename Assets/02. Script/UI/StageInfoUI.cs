using Core;
using Stage.Events;
using UnityEngine;
using UnityEngine.UI;
using War;
using War.Events;

namespace UI
{
    /// <summary>
    /// 현재 스테이지 번호와 클리어까지 남은 몬스터 수를 텍스트로 표시한다.
    /// StageChangedEvent/StageProgressChangedEvent 구독만으로 갱신되며 Stage 도메인을 직접 참조하지 않는다.
    /// 다만 War 클라이맥스에서 전멸이 아닌 목표(구조물 점령/보스 처치/수하물 보호)가 활성화되면
    /// 실제 클리어 조건과 무관한 "남은 몬스터" 수치가 오해를 줄 수 있어 그 줄만 숨긴다.
    /// StageChangedEvent.IsBreakthrough를 색이 다른 "[돌파]"/"[반복]" 뱃지로 붙여, 플레이어가 지금
    /// 새로운 스테이지에 도전 중인지 이미 깬 곳을 반복 중인지 한눈에 구분할 수 있게 한다.
    /// </summary>
    public sealed class StageInfoUI : MonoBehaviour
    {
        private const string BreakthroughBadge = "<color=#D3B23D>[돌파]</color>";
        private const string RepeatBadge = "<color=#AAAAAA>[반복]</color>";

        [SerializeField]
        private Text stageInfoText;

        private int _chapter;
        private int _stageNumber;
        private int _remainingCount;
        private int _totalCount;
        private bool _hideMonsterCount;
        private bool _isBreakthrough;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Subscribe<StageProgressChangedEvent>(OnStageProgressChanged);
            GameBootstrapper.Events?.Subscribe<WarClimaxWarmupStartedEvent>(OnWarClimaxWarmupStarted);
            GameBootstrapper.Events?.Subscribe<WarClimaxStateChangedEvent>(OnWarClimaxStateChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Unsubscribe<StageProgressChangedEvent>(OnStageProgressChanged);
            GameBootstrapper.Events?.Unsubscribe<WarClimaxWarmupStartedEvent>(OnWarClimaxWarmupStarted);
            GameBootstrapper.Events?.Unsubscribe<WarClimaxStateChangedEvent>(OnWarClimaxStateChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _chapter = evt.Chapter;
            _stageNumber = evt.StageNumber;
            _isBreakthrough = evt.IsBreakthrough;
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
            _hideMonsterCount = evt.ObjectiveType != WarObjectiveType.Annihilation;
            Refresh();
        }

        private void OnWarClimaxStateChanged(WarClimaxStateChangedEvent evt)
        {
            _hideMonsterCount = evt.IsClimax && evt.ObjectiveType != WarObjectiveType.Annihilation;
            Refresh();
        }

        private void Refresh()
        {
            string badge = _isBreakthrough ? BreakthroughBadge : RepeatBadge;

            stageInfoText.text = _hideMonsterCount
                ? $"스테이지 {_chapter}-{_stageNumber} {badge}"
                : $"스테이지 {_chapter}-{_stageNumber} {badge}\n남은 몬스터: {_remainingCount}/{_totalCount}";
        }
    }
}
