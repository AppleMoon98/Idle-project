namespace Rank.Events
{
    /// <summary>
    /// 랭크 승급전 세션이 완전히 종료돼(승급이든, 실패 후 나가기든) 원래 스테이지로 복귀했음을 알린다.
    /// </summary>
    public readonly struct RankPromotionSessionEndedEvent
    {
        public bool Promoted { get; }

        public RankPromotionSessionEndedEvent(bool promoted)
        {
            Promoted = promoted;
        }
    }
}
