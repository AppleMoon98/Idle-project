using UnityEngine;

namespace Core.Pooling
{
    /// <summary>
    /// PoolManager가 스폰한 인스턴스에 자동으로 부착하는 내부 태그 컴포넌트.
    /// Release 시 호출자가 원본 프리팹을 몰라도 올바른 풀로 반납할 수 있도록 출처를 기록한다.
    /// </summary>
    internal sealed class PooledInstance : MonoBehaviour
    {
        /// <summary>
        /// 이 인스턴스가 어느 프리팹의 풀에서 생성되었는지를 나타낸다.
        /// </summary>
        public GameObject SourcePrefab { get; set; }
    }
}
