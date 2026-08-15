using UnityEngine;

namespace Dungeon.Events
{
    /// <summary>
    /// 강화석 던전 보스전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다. BossInstance는
    /// UI.StoneDungeonHudUI가 보스 전용 체력바를 그 인스턴스로 필터링(Character.Events.
    /// CharacterHealthChangedEvent)하기 위한 것 — 이 시점에 스폰이 이미 끝나 있으므로(StartAttempt가
    /// SpawnBoss 이후 발행) 항상 유효한 참조다.
    /// </summary>
    public readonly struct StoneDungeonAttemptStartedEvent
    {
        public int StageNumber { get; }
        public float TimeLimitSeconds { get; }
        public GameObject BossInstance { get; }

        public StoneDungeonAttemptStartedEvent(int stageNumber, float timeLimitSeconds, GameObject bossInstance)
        {
            StageNumber = stageNumber;
            TimeLimitSeconds = timeLimitSeconds;
            BossInstance = bossInstance;
        }
    }
}
