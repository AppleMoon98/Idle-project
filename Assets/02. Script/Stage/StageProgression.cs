using Character.Events;
using Core;
using Stage.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 진행 정책을 담당한다. "현재 스테이지"와 "역대 최고 클리어 스테이지"를
    /// 카탈로그 인덱스로 관리한다:
    ///
    /// - 클리어 시: 새 기록이면(현재 &gt; 최고) 항상 최고 기록을 갱신한다(모드와 무관 — 반복
    ///   모드에서도 진짜 기록이므로 잃지 않는다). 전진 여부는 StageModeService.CurrentMode를
    ///   따른다 — Breakthrough면 기존과 동일하게 다음 스테이지로(다음이 없으면 최고 기록 자리를
    ///   반복), Repeat이면 전진하지 않고 같은 스테이지를 계속 반복한다.
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
        private readonly StageModeService _modeService;

        private int _currentIndex;
        private int _highestClearedIndex;
        private bool _isSuppressed;

        public StageProgression(
            StageCatalogSO catalog,
            StageController controller,
            EventBus events,
            Transform playerTransform,
            int maxRegressionDistance,
            StageSO initialCurrentStage,
            StageSO initialHighestClearedStage,
            StageModeService modeService)
        {
            _catalog = catalog;
            _controller = controller;
            _events = events;
            _playerTransform = playerTransform;
            _maxRegressionDistance = maxRegressionDistance;
            _modeService = modeService;

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

        /// <summary>
        /// 현재 플레이어가 선택한 방침이 돌파인지(true) 반복인지(false). StageModeService가 없으면
        /// (구성 실수 등 방어적 상황) 기존 기본 동작인 돌파로 취급한다.
        /// </summary>
        public bool IsBreakthrough => _modeService == null || _modeService.CurrentMode == StageProgressionMode.Breakthrough;

        /// <summary>
        /// true인 동안 StageClearedEvent/Player CharacterDiedEvent를 완전히 무시한다. 던전 등
        /// 오버레이가 화면을 차지하는 동안(StageController.PauseForOverlay~ResumeAfterOverlay)
        /// 그 안에서 벌어지는 죽음/클리어가 실제 스테이지 진행도(돌파/후퇴)에 새어 들어가
        /// LoadStage를 몰래 호출해버리는 것을 막기 위한 것 — 이게 없으면 오버레이 도중 Player가
        /// 죽었을 때 여기서 스테이지를 다시 로드해 잔몹이 오버레이 위에 스폰되는 문제가 있었다.
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            _isSuppressed = suppressed;
        }

        private void OnStageCleared(StageClearedEvent evt)
        {
            if (_isSuppressed)
            {
                return;
            }

            if (_currentIndex > _highestClearedIndex)
            {
                _highestClearedIndex = _currentIndex;
                StageSO highest = _catalog.GetAt(_highestClearedIndex);
                _events.Publish(new HighestStageClearedEvent(highest.Chapter, highest.StageNumber));
            }

            if (IsBreakthrough)
            {
                StageSO next = _catalog.GetAt(_currentIndex + 1);
                _currentIndex = next != null ? _currentIndex + 1 : _highestClearedIndex;
            }

            _controller.LoadStage(_catalog.GetAt(_currentIndex));
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (_isSuppressed)
            {
                return;
            }

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
