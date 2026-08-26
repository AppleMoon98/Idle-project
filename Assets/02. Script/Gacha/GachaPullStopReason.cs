namespace Gacha
{
    /// <summary>
    /// Pull() 계열 메서드가 요청한 횟수(count)보다 적게 실행하고 멈췄을 때, 그 이유를 나타낸다
    /// (GitHub 이슈 #22 - "300회 요청했는데 몇 회가, 왜 실행이 안 됐는지 안내가 없다"). 각
    /// TryPullOne 계열 메서드가 실패할 때 이 값을 함께 반환해, Pull()이 정확한 안내 토스트를
    /// 구성할 수 있게 한다.
    /// </summary>
    public enum GachaPullStopReason
    {
        /// <summary>
        /// 멈추지 않았다(요청한 횟수를 전부 성공했거나, 아직 실패한 적이 없다).
        /// </summary>
        None,

        /// <summary>
        /// 다음 1회분 재화(골드/소환권/주문서/뽑기권)가 모자라 멈췄다.
        /// </summary>
        InsufficientCurrency,

        /// <summary>
        /// 재화는 충분하지만 뽑기 후보 데이터 자체가 없어(확률 테이블/카탈로그가 비어있음, 잘못된
        /// tierIndex) 멈췄다 - 콘텐츠/설정 오류다(GitHub 이슈 #22, 이전엔 AllCandidatesMaxed와
        /// 하나로 뭉쳐 있어 "스킬이 전부 만렙이라 정상적으로 멈춘 것"과 "테이블이 비어있는 버그"가
        /// 사용자에게 똑같은 메시지로 보였다 - 후자는 QA가 진단할 수 있어야 하는 별개의 상태다.
        /// </summary>
        NoCandidates,

        /// <summary>
        /// 뽑기 후보 데이터는 정상적으로 있지만, 그 안의 모든 항목이 이미 최대 레벨(또는 그에
        /// 준하는 "더 이상 뽑을 이유가 없는" 상태)이라 멈췄다 - 콘텐츠 오류가 아니라 플레이어가
        /// 실제로 도달할 수 있는 정상적인 성장 완료 상태다. 현재는 SkillGachaService(스킬 레벨
        /// 상한)만 이 값을 반환한다 - 병사/장비 가챠는 "만렙" 개념 자체가 없어 후보가 비면 항상
        /// NoCandidates(데이터 오류)다.
        /// </summary>
        AllCandidatesMaxed
    }
}
