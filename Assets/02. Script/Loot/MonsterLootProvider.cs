using UnityEngine;

namespace Loot
{
    /// <summary>
    /// 몬스터 프리팹에 부착해 해당 몬스터의 드롭 데이터를 노출하는 컴포넌트.
    /// LootDropper가 사망한 GameObject에서 이 컴포넌트 존재 여부로 몬스터인지 판별한다.
    /// </summary>
    public sealed class MonsterLootProvider : MonoBehaviour
    {
        [SerializeField]
        private MonsterLootSO loot;

        /// <summary>
        /// 이 몬스터의 드롭 데이터.
        /// </summary>
        public MonsterLootSO Loot => loot;
    }
}
