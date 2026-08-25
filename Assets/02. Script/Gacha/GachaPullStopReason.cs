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
        /// 재화는 충분하지만 뽑을 수 있는 후보가 없어(예: 스킬이 전부 최대 레벨, 확률 테이블이
        /// 비어있음) 멈췄다.
        /// </summary>
        NoCandidates
    }
}
