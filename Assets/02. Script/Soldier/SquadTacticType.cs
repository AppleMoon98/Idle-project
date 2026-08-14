namespace Soldier
{
    /// <summary>
    /// 부대(SoldierDeploymentService의 SquadCount개) 단위로 선택하는 전술. None은 전술 없음(자유
    /// 행동, SquadMovementSyncService의 부대 이동속도 동기화만 적용). 배치에만 영향을 주는
    /// "형성형" 전술(ShieldWall)과, 맵 밖에 숨어있다 스테이지 시작 후 일정 시간 뒤 등장하는
    /// "특수 기동형" 전술(LeftRightRaid/RearRaid)을 같은 enum 아래 나열한다 — SquadTacticService는
    /// 어떤 부대가 어떤 전술을 골랐는지만 저장하고, 실제 효과는 그 전술을 구독하는 별도 조율자
    /// (예: SquadShieldWallCoordinator, SquadRaidCoordinator)가 담당한다.
    /// </summary>
    public enum SquadTacticType
    {
        None,
        ShieldWall,
        LeftRightRaid,
        RearRaid,
    }
}
