namespace Dungeon.Events
{
    /// <summary>
    /// 골드 던전을 전멸 클리어했을 때(시간 초과 실패는 제외) 발행된다. 결과 팝업
    /// (UI.GoldDungeonClearPopupUI)이 이 값들로 요약 텍스트를 직접 구성한다.
    /// </summary>
    public readonly struct GoldDungeonClearedEvent
    {
        public int Chapter { get; }
        public int StageNumber { get; }
        public float ElapsedSeconds { get; }
        public int TotalGoldEarned { get; }

        public GoldDungeonClearedEvent(int chapter, int stageNumber, float elapsedSeconds, int totalGoldEarned)
        {
            Chapter = chapter;
            StageNumber = stageNumber;
            ElapsedSeconds = elapsedSeconds;
            TotalGoldEarned = totalGoldEarned;
        }
    }
}
