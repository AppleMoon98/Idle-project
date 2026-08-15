namespace UI.Events
{
    /// <summary>
    /// 화면 어디서든 잠깐 보였다 사라지는 안내 메시지(토스트)를 띄워달라는 요청. 어떤 도메인이든
    /// (던전 입장 거부, 재료 부족 등) 이 이벤트만 발행하면 되고, 실제로 화면에 그리는 책임은
    /// 전역으로 하나만 존재하는 UI.TemporaryMessageUI가 진다. type을 생략하면 Warning(기존
    /// 붉은 계열 색상) — 지금까지의 호출부가 전부 거부/부족 안내였으므로 기본값을 그대로 유지한다.
    /// </summary>
    public readonly struct ToastMessageRequestedEvent
    {
        public string Message { get; }
        public ToastMessageType Type { get; }

        public ToastMessageRequestedEvent(string message, ToastMessageType type = ToastMessageType.Warning)
        {
            Message = message;
            Type = type;
        }
    }
}
