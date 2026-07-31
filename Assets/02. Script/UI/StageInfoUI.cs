using Core;
using Stage.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 현재 스테이지 번호와 클리어까지 남은 몬스터 수를 텍스트로 표시한다.
    /// StageChangedEvent/StageProgressChangedEvent 구독만으로 갱신되며 Stage 도메인을 직접 참조하지 않는다.
    /// </summary>
    public sealed class StageInfoUI : MonoBehaviour
    {
        [SerializeField]
        private Text stageInfoText;

        private int _chapter;
        private int _stageNumber;
        private int _remainingCount;
        private int _totalCount;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Subscribe<StageProgressChangedEvent>(OnStageProgressChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Unsubscribe<StageProgressChangedEvent>(OnStageProgressChanged);
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

        private void Refresh()
        {
            stageInfoText.text = $"스테이지 {_chapter}-{_stageNumber}\n남은 몬스터: {_remainingCount}/{_totalCount}";
        }
    }
}
