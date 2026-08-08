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

        /// <summary>
        /// 돌파(새로운 스테이지에 처음 도전)인지 반복(이미 클리어한 스테이지를 다시 도는 중)인지.
        /// StageProgression의 "current > highest" 판정을 그대로 실어 보낸다 — UI 등 소비자가
        /// Stage 도메인을 직접 참조하지 않고도 모드를 표시할 수 있게 하기 위함.
        /// </summary>
        public bool IsBreakthrough { get; }

        public StageChangedEvent(int chapter, int stageNumber, bool isBreakthrough)
        {
            Chapter = chapter;
            StageNumber = stageNumber;
            IsBreakthrough = isBreakthrough;
        }
    }
}
