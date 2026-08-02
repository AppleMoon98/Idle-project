namespace War.Events
{
    /// <summary>
    /// 활성 War 목표의 진행도(0~1)가 바뀌었을 때 발행되는 이벤트. 구조물 점령/수하물 보호처럼
    /// 연속적 진행도가 있는 목표의 게이지 UI가 구독한다.
    /// </summary>
    public readonly struct WarObjectiveProgressChangedEvent
    {
        /// <summary>
        /// 현재 진행도(0~1).
        /// </summary>
        public float Progress01 { get; }

        public WarObjectiveProgressChangedEvent(float progress01)
        {
            Progress01 = progress01;
        }
    }
}
