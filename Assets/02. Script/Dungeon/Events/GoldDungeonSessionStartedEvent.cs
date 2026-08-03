namespace Dungeon.Events
{
    /// <summary>
    /// 골드 던전 세션이 시작됐음을 알린다.
    /// </summary>
    public readonly struct GoldDungeonSessionStartedEvent
    {
        public int StageNumber { get; }
        public int TotalMonsters { get; }
        public float TimeLimitSeconds { get; }

        public GoldDungeonSessionStartedEvent(int stageNumber, int totalMonsters, float timeLimitSeconds)
        {
            StageNumber = stageNumber;
            TotalMonsters = totalMonsters;
            TimeLimitSeconds = timeLimitSeconds;
        }
    }
}
