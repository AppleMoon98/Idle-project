namespace Rank.Events
{
    /// <summary>
    /// 랭크가 승급(또는 세이브 복원으로 초기 반영)되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct RankChangedEvent
    {
        /// <summary>
        /// 갱신된 현재 랭크.
        /// </summary>
        public RankSO NewRank { get; }

        /// <summary>
        /// 갱신된 현재 랭크의 RankCatalogSO 상 인덱스.
        /// </summary>
        public int NewRankIndex { get; }

        /// <summary>
        /// 세이브 복원(RestoreLevel)으로 인한 발행이면 true, 실제 스테이지 클리어로 인한 신규
        /// 승급이면 false. 승급 알림 팝업처럼 "진짜 방금 승급했다"만 반응해야 하는 구독자가
        /// 앱 시작 시 복원 이벤트까지 알림으로 띄우지 않도록 구분한다.
        /// </summary>
        public bool IsRestore { get; }

        public RankChangedEvent(RankSO newRank, int newRankIndex, bool isRestore)
        {
            NewRank = newRank;
            NewRankIndex = newRankIndex;
            IsRestore = isRestore;
        }
    }
}
