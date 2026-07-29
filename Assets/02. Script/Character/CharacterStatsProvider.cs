using UnityEngine;

namespace Character
{
    /// <summary>
    /// CharacterStatsSO를 RuntimeStats로 변환해 같은 GameObject의 다른 컴포넌트에 제공한다.
    /// </summary>
    public sealed class CharacterStatsProvider : MonoBehaviour
    {
        [SerializeField]
        private CharacterStatsSO baseStats;

        private RuntimeStats _stats;

        /// <summary>
        /// 이 캐릭터의 런타임 스탯. 최초 접근 시 baseStats로부터 생성된다.
        /// 컴포넌트 간 Awake 실행 순서에 의존하지 않도록 지연 생성한다.
        /// </summary>
        public RuntimeStats Stats => _stats ??= new RuntimeStats(baseStats);
    }
}
