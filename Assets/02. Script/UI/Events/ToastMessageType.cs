namespace UI.Events
{
    /// <summary>
    /// ToastMessageRequestedEvent가 어떤 성격의 메시지인지 나타낸다. UI.TemporaryMessageUI가
    /// 타입별로 다른 글자색을 적용한다 — Warning(경고/거부)은 기존 붉은 계열, Info(안내)는 흰색.
    /// </summary>
    public enum ToastMessageType
    {
        Warning,
        Info
    }
}
