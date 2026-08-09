namespace Dungeon.Events
{
    /// <summary>
    /// 3개 점령 구역의 평균 점령 진행도(0~1)가 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct SoldierRescueDungeonProgressChangedEvent
    {
        public float Progress01 { get; }

        public SoldierRescueDungeonProgressChangedEvent(float progress01)
        {
            Progress01 = progress01;
        }
    }
}
