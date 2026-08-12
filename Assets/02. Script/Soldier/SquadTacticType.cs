namespace Soldier
{
    /// <summary>
    /// 부대(SoldierDeploymentService의 SquadCount개) 단위로 선택하는 전술. None은 전술 없음(자유
    /// 행동, SquadMovementSyncService의 부대 이동속도 동기화만 적용). 배치에만 영향을 주는
    /// "형성형" 전술(ShieldWall)과, 이후 추가될 맵 이탈/후방 등장 같은 "특수 기동형" 전술을
    /// 같은 enum 아래 나열한다 — SquadTacticService는 어떤 부대가 어떤 전술을 골랐는지만
    /// 저장하고, 실제 효과는 그 전술을 구독하는 별도 조율자(예: SquadShieldWallCoordinator)가
    /// 담당한다.
    /// </summary>
    public enum SquadTacticType
    {
        None,
        ShieldWall,
    }
}
