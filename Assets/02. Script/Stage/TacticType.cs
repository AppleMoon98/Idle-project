namespace Stage
{
    /// <summary>
    /// 진형 전술(Tactic)의 종류. 각 값은 Stage.Tactics.ITacticSpawnStrategy 구현체 하나에 대응된다.
    /// </summary>
    public enum TacticType
    {
        /// <summary>
        /// 방패벽 전술 - 방패병(1열)과 창병(2열)이 짝을 지어 등장한다.
        /// </summary>
        ShieldWall
    }
}
