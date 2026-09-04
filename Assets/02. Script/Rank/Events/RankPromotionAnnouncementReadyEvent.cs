namespace Rank.Events
{
    /// <summary>
    /// 승급 스토리(있다면)가 끝난 뒤, 또는 스토리가 아예 없어서 즉시 "승급 알림을 지금 보여줘도
    /// 된다"는 신호로 Rank.RankPromotionStoryGate가 발행하는 이벤트. UI.RankUpPopupUI가
    /// RankChangedEvent 대신 이 이벤트를 구독한다 - 스토리 재생 순서를 두 컴포넌트가 서로 직접
    /// 참조하지 않고도 EventBus 재발행만으로 보장한다(Stage.Events.HighestStageClearedEvent가
    /// StageChangedEvent에서 갈라져 나온 것과 같은 관례).
    /// </summary>
    public readonly struct RankPromotionAnnouncementReadyEvent
    {
        /// <summary>
        /// 승급된 새 랭크.
        /// </summary>
        public RankSO NewRank { get; }

        /// <summary>
        /// 승급된 새 랭크의 RankCatalogSO 상 인덱스.
        /// </summary>
        public int NewRankIndex { get; }

        public RankPromotionAnnouncementReadyEvent(RankSO newRank, int newRankIndex)
        {
            NewRank = newRank;
            NewRankIndex = newRankIndex;
        }
    }
}
