namespace Dungeon.Events
{
    /// <summary>
    /// 골드 던전 세션 중 남은 몬스터 수가 바뀔 때마다(스폰 직후, 처치할 때마다) 발행된다.
    /// </summary>
    public readonly struct GoldDungeonProgressChangedEvent
    {
        public int RemainingMonsters { get; }

        public GoldDungeonProgressChangedEvent(int remainingMonsters)
        {
            RemainingMonsters = remainingMonsters;
        }
    }
}
