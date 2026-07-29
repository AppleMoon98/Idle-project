namespace Core.Pooling
{
    /// <summary>
    /// 오브젝트 풀에서 스폰/디스폰될 때 상태를 초기화/정리하기 위한 훅 인터페이스.
    /// 풀링 대상 컴포넌트가 필요한 경우에만 선택적으로 구현한다.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 풀에서 꺼내져 활성화된 직후 호출된다.
        /// </summary>
        void OnSpawned();

        /// <summary>
        /// 풀로 반납되어 비활성화되기 직전에 호출된다.
        /// </summary>
        void OnDespawned();
    }
}
