namespace Character.Events
{
    /// <summary>
    /// 플레이어의 이동 제어 방식이 바뀌었을 때 발행되는 이벤트.
    /// </summary>
    public readonly struct PlayerControlModeChangedEvent
    {
        /// <summary>
        /// 새로 적용된 제어 방식.
        /// </summary>
        public PlayerControlMode Mode { get; }

        public PlayerControlModeChangedEvent(PlayerControlMode mode)
        {
            Mode = mode;
        }
    }
}
