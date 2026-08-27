namespace Enhancement
{
    /// <summary>
    /// EnhancementService/SoldierEnhancement.SoldierEnhancementService.RestoreLevel의 결과
    /// (GitHub 이슈 #50). 호출부(GameBootstrapper)가 ClampedToMax일 때만 진단 로그를 남길 수 있게
    /// 세 가지로 구분한다 - 정상 복원과 손상된 저장값을 보정한 경우를 구별해야 "폐기/보정된 값을
    /// 구조화된 진단으로 남겨야 한다"는 완료 조건을 충족할 수 있다.
    /// </summary>
    public enum LevelRestoreOutcome
    {
        /// <summary>레벨이 [0, MaxLevel] 범위 안이라 그대로 적용됨.</summary>
        Applied,

        /// <summary>저장된 레벨이 MaxLevel을 넘어(또는 int.MaxValue처럼 명백히 손상돼) MaxLevel로 잘렸음.</summary>
        ClampedToMax,

        /// <summary>statType에 대응하는 EnhancementConfigSO 자체가 없어 아무것도 하지 않음.</summary>
        ConfigMissing
    }
}
