namespace Character
{
    /// <summary>
    /// 플레이어 이동 제어 방식. War 보스전 여부와 무관하게 유저가 언제든 토글할 수 있다.
    /// </summary>
    public enum PlayerControlMode
    {
        /// <summary>
        /// 기존 EnemyTracker가 자동으로 가장 가까운 적을 쫓아간다.
        /// </summary>
        Auto,

        /// <summary>
        /// 유저가 화면을 탭한 위치로 직접 이동한다. 공격은 여전히 Attacker가 자동으로 수행한다.
        /// </summary>
        Manual
    }
}
