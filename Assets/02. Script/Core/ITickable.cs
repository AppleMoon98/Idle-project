namespace Core
{
    /// <summary>
    /// 매 프레임 갱신이 필요한 대상의 계약.
    /// 개별 MonoBehaviour의 Update() 대신 GameTicker에 등록되어 일괄 갱신된다.
    /// </summary>
    public interface ITickable
    {
        /// <summary>
        /// GameTicker에 의해 매 프레임 호출된다.
        /// </summary>
        /// <param name="deltaTime">이전 프레임과의 경과 시간(초)</param>
        void Tick(float deltaTime);
    }
}
