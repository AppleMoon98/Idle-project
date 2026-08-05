namespace Rank.Events
{
    /// <summary>
    /// 랭크 승급전 도중 플레이어가 죽어 이번 시도가 실패했음을 알린다. 세션 자체는 아직
    /// 끝나지 않았고("승급 실패" 화면에서 재도전/나가기 대기 중) 원래 스테이지로는 복귀하지 않는다.
    /// </summary>
    public readonly struct RankPromotionAttemptFailedEvent
    {
    }
}
