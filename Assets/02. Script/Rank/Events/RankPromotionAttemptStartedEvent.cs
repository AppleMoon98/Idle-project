using UnityEngine;

namespace Rank.Events
{
    /// <summary>
    /// 랭크 승급전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다. BossInstance는
    /// UI.RankPromotionHudUI가 보스 전용 체력바를 그 인스턴스로 필터링(Character.Events.
    /// CharacterHealthChangedEvent)하기 위한 것 — Dungeon.Events.BossDungeonAttemptStartedEvent와
    /// 동일한 목적. 이 시점에 스폰이 이미 끝나 있으므로 항상 유효한 참조다.
    /// </summary>
    public readonly struct RankPromotionAttemptStartedEvent
    {
        public RankSO TargetRank { get; }
        public GameObject BossInstance { get; }

        public RankPromotionAttemptStartedEvent(RankSO targetRank, GameObject bossInstance)
        {
            TargetRank = targetRank;
            BossInstance = bossInstance;
        }
    }
}
