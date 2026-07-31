namespace Save
{
    /// <summary>
    /// PlayerPrefs에 저장/로드되는 최소 게임 상태 스냅샷.
    /// 오프라인 보상 계산에 필요한 값만 담는다(전체 게임 상태 저장이 아님).
    /// </summary>
    public readonly struct SaveData
    {
        /// <summary>
        /// 마지막으로 저장된 보유 골드.
        /// </summary>
        public int Gold { get; }

        /// <summary>
        /// 마지막으로 저장된 보유 강화석.
        /// </summary>
        public int EnhancementStones { get; }

        /// <summary>
        /// 마지막으로 저장된, 현재 진행 중인(도전/반복 대상) 챕터 번호.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// 마지막으로 저장된, 현재 진행 중인(도전/반복 대상) 챕터 내 스테이지 번호.
        /// </summary>
        public int StageNumber { get; }

        /// <summary>
        /// 역대 최고로 클리어한 챕터 번호. 사망으로 후퇴해도 낮아지지 않는다. 기록이 없으면 0.
        /// </summary>
        public int HighestClearedChapter { get; }

        /// <summary>
        /// 역대 최고로 클리어한 챕터 내 스테이지 번호. 기록이 없으면 0.
        /// </summary>
        public int HighestClearedStageNumber { get; }

        /// <summary>
        /// 마지막 저장 시각(UTC 유닉스 타임, 초). 저장 기록이 없으면 0.
        /// </summary>
        public long LastActiveUnixTime { get; }

        /// <summary>
        /// 마지막으로 저장된 공격력 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int AttackPowerLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 최대체력 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int MaxHealthLevel { get; }

        public SaveData(
            int gold,
            int enhancementStones,
            int chapter,
            int stageNumber,
            int highestClearedChapter,
            int highestClearedStageNumber,
            long lastActiveUnixTime,
            int attackPowerLevel,
            int maxHealthLevel)
        {
            Gold = gold;
            EnhancementStones = enhancementStones;
            Chapter = chapter;
            StageNumber = stageNumber;
            HighestClearedChapter = highestClearedChapter;
            HighestClearedStageNumber = highestClearedStageNumber;
            LastActiveUnixTime = lastActiveUnixTime;
            AttackPowerLevel = attackPowerLevel;
            MaxHealthLevel = maxHealthLevel;
        }
    }
}
