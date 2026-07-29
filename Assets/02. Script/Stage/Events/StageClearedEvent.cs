namespace Stage.Events
{
    /// <summary>
    /// 스테이지의 모든 몬스터가 처치되어 클리어되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct StageClearedEvent
    {
        /// <summary>
        /// 클리어된 스테이지.
        /// </summary>
        public StageSO Stage { get; }

        public StageClearedEvent(StageSO stage)
        {
            Stage = stage;
        }
    }
}
