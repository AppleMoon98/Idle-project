namespace Story.Events
{
    /// <summary>
    /// 게임 최초 실행 인트로 스토리를 끝까지 보거나 스킵해 완료했을 때 발행되는 이벤트.
    /// Save.SaveService가 구독해 SaveData.HasSeenIntroStory를 true로 갱신·저장한다
    /// (Stage.Events.HighestStageClearedEvent 등과 같은 "이벤트로만 세이브 상태를 갱신" 관례,
    /// section O 참고).
    /// </summary>
    public readonly struct IntroStoryCompletedEvent
    {
    }
}
