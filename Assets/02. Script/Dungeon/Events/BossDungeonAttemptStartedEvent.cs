using Rank;
using UnityEngine;

namespace Dungeon.Events
{
    /// <summary>
    /// 보스 던전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다. BossInstance는
    /// UI.BossDungeonHudUI가 보스 전용 체력바를 그 인스턴스로 필터링(Character.Events.
    /// CharacterHealthChangedEvent)하기 위한 것 — 이 시점에 스폰이 이미 끝나 있으므로 항상 유효한
    /// 참조다. SelectedRank는 어떤 승급전 보스를 골랐는지(표시 이름 등)를 UI가 알기 위한 것.
    /// </summary>
    public readonly struct BossDungeonAttemptStartedEvent
    {
        public RankSO SelectedRank { get; }
        public float TimeLimitSeconds { get; }
        public GameObject BossInstance { get; }

        public BossDungeonAttemptStartedEvent(RankSO selectedRank, float timeLimitSeconds, GameObject bossInstance)
        {
            SelectedRank = selectedRank;
            TimeLimitSeconds = timeLimitSeconds;
            BossInstance = bossInstance;
        }
    }
}
