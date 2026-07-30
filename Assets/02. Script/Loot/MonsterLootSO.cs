using UnityEngine;

namespace Loot
{
    /// <summary>
    /// 몬스터 한 종류가 사망 시 드롭하는 골드 데이터 에셋. 장비 드롭은 몬스터가 아니라
    /// 스테이지(StageSO.EquipmentDrops) 단위로 정해진다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterLoot", menuName = "Idle Project/Loot/Monster Loot")]
    public sealed class MonsterLootSO : ScriptableObject
    {
        [SerializeField]
        private int minGold;

        [SerializeField]
        private int maxGold;

        [SerializeField]
        [Range(0f, 1f)]
        private float dropChance;

        /// <summary>
        /// 최소 드롭 골드.
        /// </summary>
        public int MinGold => minGold;

        /// <summary>
        /// 최대 드롭 골드.
        /// </summary>
        public int MaxGold => maxGold;

        /// <summary>
        /// 골드가 드롭될 확률 (0~1).
        /// </summary>
        public float DropChance => dropChance;
    }
}
