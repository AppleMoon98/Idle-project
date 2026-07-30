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
        /// 마지막으로 저장된 챕터 번호.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// 마지막으로 저장된 챕터 내 스테이지 번호.
        /// </summary>
        public int StageNumber { get; }

        /// <summary>
        /// 마지막 저장 시각(UTC 유닉스 타임, 초). 저장 기록이 없으면 0.
        /// </summary>
        public long LastActiveUnixTime { get; }

        public SaveData(int gold, int chapter, int stageNumber, long lastActiveUnixTime)
        {
            Gold = gold;
            Chapter = chapter;
            StageNumber = stageNumber;
            LastActiveUnixTime = lastActiveUnixTime;
        }
    }
}
