namespace Core
{
    /// <summary>
    /// "GameBootstrapper.Services가 있고 GameTicker를 얻을 수 있으면 등록/해제한다"라는
    /// 반복 보일러플레이트를 한 줄로 줄여주는 정적 헬퍼. GameTicker.Register/Unregister 자체의
    /// 동작(순회 도중 호출해도 안전함 등)은 그대로이며, 이 헬퍼는 조회 과정만 감싼다.
    /// </summary>
    public static class TickerRegistration
    {
        /// <summary>
        /// tickable을 GameTicker에 등록한다. Services/GameTicker를 아직 쓸 수 없으면 아무 일도 하지 않는다.
        /// </summary>
        public static void Register(ITickable tickable)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(tickable);
            }
        }

        /// <summary>
        /// tickable을 GameTicker에서 해제한다. Services/GameTicker를 이미 쓸 수 없으면 아무 일도 하지 않는다.
        /// </summary>
        public static void Unregister(ITickable tickable)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(tickable);
            }
        }
    }
}
