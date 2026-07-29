namespace Core
{
    /// <summary>
    /// 상태를 가지고 생명주기 관리가 필요한 매니저의 계약.
    /// ServiceLocator에 등록되어 Bootstrapper에 의해 초기화/종료된다.
    /// </summary>
    public interface IManager
    {
        /// <summary>
        /// 매니저의 내부 상태를 초기화한다. ServiceLocator 등록 직후 호출된다.
        /// </summary>
        void Initialize();

        /// <summary>
        /// 매니저가 보유한 리소스를 해제하고 구독 중인 이벤트를 정리한다.
        /// </summary>
        void Shutdown();
    }
}
