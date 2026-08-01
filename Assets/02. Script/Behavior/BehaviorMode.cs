namespace Behavior
{
    /// <summary>
    /// 병사 행동 규칙이 선택할 수 있는 상위 행동 모드. SoldierBehaviorController가 이 값에 따라
    /// EnemyTracker/CharacterMover를 조합해 실제 움직임으로 옮긴다.
    /// </summary>
    public enum BehaviorMode
    {
        /// <summary>
        /// 교전: 가장 가까운 적을 능동적으로 찾아 접근/공격한다(기존 기본 동작).
        /// </summary>
        Engage,

        /// <summary>
        /// 제자리 사수: 새 대상을 찾아 움직이지 않지만, 사거리 안에 들어온 적은 그대로 반격한다.
        /// </summary>
        Hold,

        /// <summary>
        /// 후퇴: 지정된 후퇴 지점(대개 자신의 스폰 지점)으로 이동한다.
        /// </summary>
        Retreat
    }
}
