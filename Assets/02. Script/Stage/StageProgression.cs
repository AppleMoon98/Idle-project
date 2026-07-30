using Character.Events;
using Core;
using Stage.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 진행 정책을 담당한다. "현재 스테이지"와 "역대 최고 클리어 스테이지"를
    /// 카탈로그 인덱스로 관리하며, 둘을 비교해 돌파/반복 여부를 그때그때 판단한다
    /// (별도의 모드 상태를 저장하지 않는다):
    ///
    /// - 클리어 시: 새 기록이면(현재 == 최고+1) 최고 기록을 갱신하고 다음 스테이지로,
    ///   다음이 없으면 그 자리를 반복. 이미 클리어한 곳을 반복 클리어한 경우엔 한 칸만
    ///   전진한다 — 반복하다 최고 기록+1까지 도달하면 자연스럽게 돌파가 재개된다.
    /// - 플레이어 사망 시: 모드와 무관하게 한 칸 후퇴하되, 최고 기록 기준
    ///   maxRegressionDistance칸 밑으로는 내려가지 않는다.
    /// </summary>
    public sealed class StageProgression
    {
        private readonly StageCatalogSO _catalog;
        private readonly StageController _controller;
        private readonly EventBus _events;
        private readonly Transform _playerTransform;
        private readonly int _maxRegressionDistance;

        private int _currentIndex;
        private int _highestClearedIndex;

        public StageProgression(
            StageCatalogSO catalog,
            StageController controller,
            EventBus events,
            Transform playerTransform,
            int maxRegressionDistance,
            StageSO initialCurrentStage,
            StageSO initialHighestClearedStage)
        {
            _catalog = catalog;
            _controller = controller;
            _events = events;
            _playerTransform = playerTransform;
            _maxRegressionDistance = maxRegressionDistance;

            _currentIndex = _catalog.IndexOf(initialCurrentStage);
            _highestClearedIndex = _catalog.IndexOf(initialHighestClearedStage);

            _events.Subscribe<StageClearedEvent>(OnStageCleared);
            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. StageController가 파괴될 때 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<StageClearedEvent>(OnStageCleared);
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnStageCleared(StageClearedEvent evt)
        {
            if (_currentIndex > _highestClearedIndex)
            {
                _highestClearedIndex = _currentIndex;
                StageSO highest = _catalog.GetAt(_highestClearedIndex);
                _events.Publish(new HighestStageClearedEvent(highest.Chapter, highest.StageNumber));
            }

            StageSO next = _catalog.GetAt(_currentIndex + 1);
            _currentIndex = next != null ? _currentIndex + 1 : _highestClearedIndex;

            _controller.LoadStage(_catalog.GetAt(_currentIndex));
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character != _playerTransform.gameObject)
            {
                return;
            }

            int floor = Mathf.Max(0, _highestClearedIndex - _maxRegressionDistance);
            _currentIndex = Mathf.Max(_currentIndex - 1, floor);

            _controller.LoadStage(_catalog.GetAt(_currentIndex));
        }
    }
}
