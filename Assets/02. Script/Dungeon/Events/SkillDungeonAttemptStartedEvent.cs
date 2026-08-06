namespace Dungeon.Events
{
    /// <summary>
    /// 스킬 던전 보스전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다.
    /// </summary>
    public readonly struct SkillDungeonAttemptStartedEvent
    {
        public int StageNumber { get; }
        public float TimeLimitSeconds { get; }

        public SkillDungeonAttemptStartedEvent(int stageNumber, float timeLimitSeconds)
        {
            StageNumber = stageNumber;
            TimeLimitSeconds = timeLimitSeconds;
        }
    }
}
