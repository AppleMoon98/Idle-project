namespace Dungeon.Events
{
    /// <summary>
    /// 강화석 던전 보스를 처치해 클리어했을 때 발행된다. 결과 팝업
    /// (UI.StoneDungeonClearPopupUI)이 이 값들로 요약 텍스트를 직접 구성한다.
    /// </summary>
    public readonly struct StoneDungeonClearedEvent
    {
        public int Chapter { get; }
        public int StageNumber { get; }
        public float ElapsedSeconds { get; }
        public int TotalStonesEarned { get; }

        public StoneDungeonClearedEvent(int chapter, int stageNumber, float elapsedSeconds, int totalStonesEarned)
        {
            Chapter = chapter;
            StageNumber = stageNumber;
            ElapsedSeconds = elapsedSeconds;
            TotalStonesEarned = totalStonesEarned;
        }
    }
}
