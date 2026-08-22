namespace Soldier
{
    /// <summary>
    /// SoldierDeploymentService.TryDeploy가 실패 원인을 UI에 알려주기 위한 값 — "부대 편성"
    /// 팝업이 원인별로 다른 안내 문구를 보여줄 때 쓴다(예: CostExceeded는 "코스트가 부족합니다",
    /// NoFreeSlot은 "배치 슬롯이 부족합니다").
    /// </summary>
    public enum DeploymentFailureReason
    {
        None,
        AlreadyDeployed,
        NotInRoster,
        NoFreeSlot,
        CostExceeded
    }
}
