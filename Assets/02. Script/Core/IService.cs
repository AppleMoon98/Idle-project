namespace Core
{
    /// <summary>
    /// 최소한의 상태만 가지는 순수 기능 제공자 마커 인터페이스.
    /// IManager와 달리 생명주기 관리(Initialize/Shutdown)를 강제하지 않는다.
    /// </summary>
    public interface IService
    {
    }
}
