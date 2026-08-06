namespace Dungeon.Events
{
    /// <summary>
    /// 제한시간 안에 보스를 처치하지 못해 이번 시도가 실패했음을 알린다. 세션 자체는 아직
    /// 끝나지 않았고("토벌 실패" 화면에서 재도전/나가기 대기 중) 원래 스테이지로는 복귀하지 않는다.
    /// </summary>
    public readonly struct SkillDungeonAttemptFailedEvent
    {
    }
}
