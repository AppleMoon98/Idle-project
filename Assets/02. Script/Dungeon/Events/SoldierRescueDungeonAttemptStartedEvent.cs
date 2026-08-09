namespace Dungeon.Events
{
    /// <summary>
    /// 병사 구출 던전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다.
    /// </summary>
    public readonly struct SoldierRescueDungeonAttemptStartedEvent
    {
        public int StageNumber { get; }
        public float TimeLimitSeconds { get; }
        public int ZoneCount { get; }

        public SoldierRescueDungeonAttemptStartedEvent(int stageNumber, float timeLimitSeconds, int zoneCount)
        {
            StageNumber = stageNumber;
            TimeLimitSeconds = timeLimitSeconds;
            ZoneCount = zoneCount;
        }
    }
}
