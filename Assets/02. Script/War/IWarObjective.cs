namespace War
{
    /// <summary>
    /// War 목표 하나의 진행 상태 계약. WarBattleController가 활성 목표를 이 인터페이스로만
    /// 다루므로, 목표 종류(구조물 점령/수하물 보호)가 늘어나도 WarBattleController 쪽 판정
    /// 로직은 바뀌지 않는다.
    /// </summary>
    public interface IWarObjective
    {
        /// <summary>
        /// 목표를 달성해 스테이지를 클리어해야 하는 상태인지.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// 목표 달성에 실패해 스테이지를 후퇴시켜야 하는 상태인지.
        /// </summary>
        bool HasFailed { get; }

        /// <summary>
        /// 목표 진행도(0~1).
        /// </summary>
        float Progress01 { get; }

        /// <summary>
        /// 새로운 시도를 위해 내부 진행 상태를 초기화한다. WarBattleController가 목표를
        /// 활성화하기 직전에 호출한다.
        /// </summary>
        void ResetForNewAttempt();
    }
}
