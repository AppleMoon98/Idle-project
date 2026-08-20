namespace Dungeon.Events
{
    /// <summary>
    /// 보스 던전의 보스를 처치해 클리어했을 때 발행된다. 결과 팝업(UI.BossDungeonClearPopupUI)이
    /// 이 값들로 요약 텍스트를 직접 구성한다. 단계 스테퍼가 없는 던전이라 StoneDungeonClearedEvent와
    /// 달리 기준 스테이지 대신 처치한 보스의 랭크 표시 이름을 담는다.
    /// </summary>
    public readonly struct BossDungeonClearedEvent
    {
        public string BossDisplayName { get; }
        public float ElapsedSeconds { get; }
        public int TotalTokensEarned { get; }

        public BossDungeonClearedEvent(string bossDisplayName, float elapsedSeconds, int totalTokensEarned)
        {
            BossDisplayName = bossDisplayName;
            ElapsedSeconds = elapsedSeconds;
            TotalTokensEarned = totalTokensEarned;
        }
    }
}
