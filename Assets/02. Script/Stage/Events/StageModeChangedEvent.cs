namespace Stage.Events
{
    /// <summary>
    /// 플레이어가 스테이지 진행 방침(돌파/반복)을 바꿨을 때 발행되는 이벤트.
    /// </summary>
    public readonly struct StageModeChangedEvent
    {
        public StageProgressionMode Mode { get; }

        public StageModeChangedEvent(StageProgressionMode mode)
        {
            Mode = mode;
        }
    }
}
