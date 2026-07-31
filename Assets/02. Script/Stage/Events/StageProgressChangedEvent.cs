namespace Stage.Events
{
    /// <summary>
    /// 현재 스테이지 클리어까지 남은 처치 수가 바뀔 때마다 발행되는 이벤트.
    /// StageProgressTracker 내부 킬 카운트를 직접 참조하지 않고도 UI 등이 진행률을 표시할 수 있게 한다.
    /// </summary>
    public readonly struct StageProgressChangedEvent
    {
        /// <summary>
        /// 클리어까지 남은 처치 수.
        /// </summary>
        public int RemainingCount { get; }

        /// <summary>
        /// 이 스테이지를 클리어하는 데 필요한 전체 처치 수.
        /// </summary>
        public int TotalCount { get; }

        public StageProgressChangedEvent(int remainingCount, int totalCount)
        {
            RemainingCount = remainingCount;
            TotalCount = totalCount;
        }
    }
}
