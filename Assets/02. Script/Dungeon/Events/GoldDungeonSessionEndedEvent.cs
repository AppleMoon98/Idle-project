namespace Dungeon.Events
{
    /// <summary>
    /// 골드 던전 세션이 종료됐음을 알린다(전멸 클리어든 시간 종료든).
    /// </summary>
    public readonly struct GoldDungeonSessionEndedEvent
    {
        public bool Cleared { get; }

        public GoldDungeonSessionEndedEvent(bool cleared)
        {
            Cleared = cleared;
        }
    }
}
