using Core;
using Stage.Events;

namespace Stage
{
    /// <summary>
    /// 플레이어가 선택한 현재 스테이지 진행 방침(돌파/반복)을 보관하고, 변경 시
    /// StageModeChangedEvent를 발행한다. StageProgression(실제 적용)과 UI 토글 버튼이 이 서비스를
    /// 통해서만 상태를 주고받는다. 앱을 새로 시작할 때마다 항상 Breakthrough로 초기화되며 저장하지
    /// 않는다(의도적 — 세이브 스코프를 늘리지 않기로 결정).
    /// </summary>
    public sealed class StageModeService : IManager, IService
    {
        private readonly EventBus _events;

        public StageProgressionMode CurrentMode { get; private set; }

        public StageModeService(EventBus events)
        {
            _events = events;
            CurrentMode = StageProgressionMode.Breakthrough;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// Breakthrough/Repeat을 서로 뒤바꾼다.
        /// </summary>
        public void Toggle()
        {
            SetMode(CurrentMode == StageProgressionMode.Breakthrough ? StageProgressionMode.Repeat : StageProgressionMode.Breakthrough);
        }

        public void SetMode(StageProgressionMode mode)
        {
            if (mode == CurrentMode)
            {
                return;
            }

            CurrentMode = mode;
            _events.Publish(new StageModeChangedEvent(mode));
        }
    }
}
