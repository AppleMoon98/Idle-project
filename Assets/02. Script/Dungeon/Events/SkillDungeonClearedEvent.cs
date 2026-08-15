namespace Dungeon.Events
{
    /// <summary>
    /// 스킬 던전 보스를 처치해 클리어했을 때 발행된다. 결과 팝업
    /// (UI.SkillDungeonClearPopupUI)이 이 값들로 요약 텍스트를 직접 구성한다. SkillDungeonConfigSO는
    /// StoneDungeonConfigSO와 달리 챕터 기준 스테이지 개념이 없으므로(section BI) 단계(층) 번호만
    /// 담는다.
    /// </summary>
    public readonly struct SkillDungeonClearedEvent
    {
        public int StageNumber { get; }
        public float ElapsedSeconds { get; }
        public int TotalScrollsEarned { get; }

        public SkillDungeonClearedEvent(int stageNumber, float elapsedSeconds, int totalScrollsEarned)
        {
            StageNumber = stageNumber;
            ElapsedSeconds = elapsedSeconds;
            TotalScrollsEarned = totalScrollsEarned;
        }
    }
}
