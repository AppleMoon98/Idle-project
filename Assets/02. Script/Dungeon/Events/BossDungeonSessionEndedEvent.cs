namespace Dungeon.Events
{
    /// <summary>
    /// 보스 던전 세션이 완전히 종료돼(클리어든, 실패 후 나가기든) 원래 스테이지로 복귀했음을 알린다.
    /// </summary>
    public readonly struct BossDungeonSessionEndedEvent
    {
        public bool Cleared { get; }

        public BossDungeonSessionEndedEvent(bool cleared)
        {
            Cleared = cleared;
        }
    }
}
