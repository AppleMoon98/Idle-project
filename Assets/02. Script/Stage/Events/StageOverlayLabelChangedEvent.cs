namespace Stage.Events
{
    /// <summary>
    /// StageController.PauseForOverlay/ResumeAfterOverlay가 발행한다. 골드 던전 등 오버레이가
    /// 화면을 차지하는 동안 상단 스테이지 정보 텍스트(UI.StageInfoUI)가 평소의 "스테이지 N-M" 대신
    /// 무엇을 보여줘야 하는지를 나타낸다. Label이 null이면 오버레이가 끝나 평소 스테이지 정보로
    /// 되돌아가야 한다는 뜻이다.
    /// </summary>
    public readonly struct StageOverlayLabelChangedEvent
    {
        public readonly string Label;

        public StageOverlayLabelChangedEvent(string label)
        {
            Label = label;
        }
    }
}
