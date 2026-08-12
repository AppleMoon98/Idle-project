namespace Soldier.Events
{
    /// <summary>
    /// 부대의 전술 배정이 바뀌었을 때(SquadTacticService.SetTactic 성공 시) EventBus를 통해
    /// 발행되는 이벤트.
    /// </summary>
    public readonly struct SquadTacticChangedEvent
    {
        /// <summary>
        /// 전술이 바뀐 부대 인덱스(0 ~ SoldierDeploymentService.SquadCount - 1).
        /// </summary>
        public int SquadIndex { get; }

        /// <summary>
        /// 새로 배정된 전술.
        /// </summary>
        public SquadTacticType Tactic { get; }

        public SquadTacticChangedEvent(int squadIndex, SquadTacticType tactic)
        {
            SquadIndex = squadIndex;
            Tactic = tactic;
        }
    }
}
