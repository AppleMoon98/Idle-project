using UnityEngine;

namespace Dungeon.Events
{
    /// <summary>
    /// 스킬 던전 보스전 한 번의 시도(첫 진입이든 재도전이든)가 시작됐음을 알린다. BossInstance는
    /// UI.SkillDungeonHudUI가 보스 전용 체력바를 그 인스턴스로 필터링(Character.Events.
    /// CharacterHealthChangedEvent)하기 위한 것 — StoneDungeonAttemptStartedEvent와 동일한 이유.
    /// </summary>
    public readonly struct SkillDungeonAttemptStartedEvent
    {
        public int StageNumber { get; }
        public float TimeLimitSeconds { get; }
        public GameObject BossInstance { get; }

        public SkillDungeonAttemptStartedEvent(int stageNumber, float timeLimitSeconds, GameObject bossInstance)
        {
            StageNumber = stageNumber;
            TimeLimitSeconds = timeLimitSeconds;
            BossInstance = bossInstance;
        }
    }
}
