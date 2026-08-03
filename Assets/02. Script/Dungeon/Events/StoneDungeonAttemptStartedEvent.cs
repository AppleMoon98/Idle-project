namespace Dungeon.Events
{
    /// <summary>
    /// 강화석 던전 보스전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다.
    /// </summary>
    public readonly struct StoneDungeonAttemptStartedEvent
    {
        public int StageNumber { get; }
        public float TimeLimitSeconds { get; }

        public StoneDungeonAttemptStartedEvent(int stageNumber, float timeLimitSeconds)
        {
            StageNumber = stageNumber;
            TimeLimitSeconds = timeLimitSeconds;
        }
    }
}
