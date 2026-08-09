namespace Stage
{
    /// <summary>
    /// MonsterSpawnEntry 하나가 스폰 위치를 어떻게 정할지. 기본값(Automatic)은 기존 동작
    /// (플레이어 반대편 4방향 자동 선택)과 완전히 같다.
    /// </summary>
    public enum MonsterSpawnPlacement
    {
        /// <summary>
        /// 기존 동작 - MonsterSpawner.NextSpawnPoint()가 플레이어 반대편 방향을 자동으로 고른다.
        /// </summary>
        Automatic,

        /// <summary>
        /// 좌/우 스폰 지점 중에서만 무작위로 고른다(상/하 제외).
        /// </summary>
        LeftOrRight,

        /// <summary>
        /// 가장 최근에 배치된 전술 대형(예: 방패벽)의 후방에 스폰한다. 그 스테이지에 전술 웨이브가
        /// 없으면 Automatic으로 대체된다.
        /// </summary>
        BehindTacticFormation
    }
}
