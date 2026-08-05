namespace Rank.Events
{
    /// <summary>
    /// 랭크 승급전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다.
    /// </summary>
    public readonly struct RankPromotionAttemptStartedEvent
    {
        public RankSO TargetRank { get; }

        public RankPromotionAttemptStartedEvent(RankSO targetRank)
        {
            TargetRank = targetRank;
        }
    }
}
