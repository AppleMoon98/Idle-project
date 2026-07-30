namespace Stage.Events
{
    /// <summary>
    /// 새 스테이지가 로드되어 진행 중인 스테이지가 바뀌었을 때 발행되는 이벤트.
    /// 저장(Save) 등 "현재 스테이지가 몇 번인지"만 필요한 시스템이 Stage를 직접 참조하지 않고 구독한다.
    /// </summary>
    public readonly struct StageChangedEvent
    {
        /// <summary>
        /// 새로 로드된 스테이지의 챕터 번호.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// 새로 로드된 스테이지의 챕터 내 스테이지 번호.
        /// </summary>
        public int StageNumber { get; }

        public StageChangedEvent(int chapter, int stageNumber)
        {
            Chapter = chapter;
            StageNumber = stageNumber;
        }
    }
}
