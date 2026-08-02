using Character.Events;
using Core;

namespace Character
{
    /// <summary>
    /// 플레이어의 현재 이동 제어 방식(자동/수동)을 보관하고, 변경 시 PlayerControlModeChangedEvent를
    /// 발행한다. PlayerManualMover(제어권 실제 적용)와 UI 토글 버튼이 이 서비스를 통해서만 상태를
    /// 주고받는다.
    /// </summary>
    public sealed class PlayerControlModeService : IManager, IService
    {
        private readonly EventBus _events;

        public PlayerControlMode CurrentMode { get; private set; }

        public PlayerControlModeService(EventBus events)
        {
            _events = events;
            CurrentMode = PlayerControlMode.Auto;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// Auto/Manual을 서로 뒤바꾼다.
        /// </summary>
        public void Toggle()
        {
            SetMode(CurrentMode == PlayerControlMode.Auto ? PlayerControlMode.Manual : PlayerControlMode.Auto);
        }

        public void SetMode(PlayerControlMode mode)
        {
            if (mode == CurrentMode)
            {
                return;
            }

            CurrentMode = mode;
            _events.Publish(new PlayerControlModeChangedEvent(mode));
        }
    }
}
