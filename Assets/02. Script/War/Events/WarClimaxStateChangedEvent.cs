namespace War.Events
{
    /// <summary>
    /// 챕터 클라이맥스(War) 스테이지 진입/이탈 시 발행되는 이벤트. UI가 배너를 표시하기 위해
    /// 구독한다 — WarBattleController를 직접 참조하지 않는다.
    /// </summary>
    public readonly struct WarClimaxStateChangedEvent
    {
        /// <summary>
        /// 클라이맥스 스테이지에 진입했는지(true) 이탈했는지(false).
        /// </summary>
        public bool IsClimax { get; }

        /// <summary>
        /// 이번 클라이맥스에 배정된 목표 종류. IsClimax가 false면 의미 없다.
        /// </summary>
        public WarObjectiveType ObjectiveType { get; }

        /// <summary>
        /// 클라이맥스가 발생한 챕터.
        /// </summary>
        public int Chapter { get; }

        public WarClimaxStateChangedEvent(bool isClimax, WarObjectiveType objectiveType, int chapter)
        {
            IsClimax = isClimax;
            ObjectiveType = objectiveType;
            Chapter = chapter;
        }
    }
}
