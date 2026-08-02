namespace War.Events
{
    /// <summary>
    /// 클라이맥스 스테이지에 진입해 워밍업(경고 카운트다운)이 시작되었을 때 발행되는 이벤트.
    /// 이 시점에는 아직 목표가 활성화되지 않았으며, Duration 이후 WarClimaxStateChangedEvent(true)가
    /// 뒤따라 발행되면서 실제 목표 판정이 시작된다.
    /// </summary>
    public readonly struct WarClimaxWarmupStartedEvent
    {
        /// <summary>
        /// 워밍업이 끝난 뒤 활성화될 목표 종류.
        /// </summary>
        public WarObjectiveType ObjectiveType { get; }

        /// <summary>
        /// 클라이맥스가 발생한 챕터.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// 워밍업 지속 시간(초).
        /// </summary>
        public float Duration { get; }

        public WarClimaxWarmupStartedEvent(WarObjectiveType objectiveType, int chapter, float duration)
        {
            ObjectiveType = objectiveType;
            Chapter = chapter;
            Duration = duration;
        }
    }
}
