namespace Dungeon.Events
{
    /// <summary>
    /// 병사 구출 던전의 구역을 전부 점령해 클리어했을 때 발행된다. 결과 팝업
    /// (UI.SoldierRescueDungeonClearPopupUI)이 이 값들로 요약 텍스트를 직접 구성한다.
    /// </summary>
    public readonly struct SoldierRescueDungeonClearedEvent
    {
        public int Chapter { get; }
        public int StageNumber { get; }
        public float ElapsedSeconds { get; }
        public int TotalTicketsEarned { get; }

        public SoldierRescueDungeonClearedEvent(int chapter, int stageNumber, float elapsedSeconds, int totalTicketsEarned)
        {
            Chapter = chapter;
            StageNumber = stageNumber;
            ElapsedSeconds = elapsedSeconds;
            TotalTicketsEarned = totalTicketsEarned;
        }
    }
}
