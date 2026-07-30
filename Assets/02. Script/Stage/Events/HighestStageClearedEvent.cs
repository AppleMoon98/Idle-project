namespace Stage.Events
{
    /// <summary>
    /// 역대 최고 클리어 스테이지 기록이 갱신되었을 때 EventBus를 통해 발행되는 이벤트.
    /// 현재 진행 위치(StageChangedEvent)와는 별개로, 사망으로 후퇴해도 절대 낮아지지 않는
    /// "최고 기록"만을 담는다. 저장(Save)과 오프라인 보상 계산의 기준점이 된다.
    /// </summary>
    public readonly struct HighestStageClearedEvent
    {
        /// <summary>
        /// 새로 갱신된 최고 기록의 챕터 번호.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// 새로 갱신된 최고 기록의 챕터 내 스테이지 번호.
        /// </summary>
        public int StageNumber { get; }

        public HighestStageClearedEvent(int chapter, int stageNumber)
        {
            Chapter = chapter;
            StageNumber = stageNumber;
        }
    }
}
