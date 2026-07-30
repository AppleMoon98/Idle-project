using UnityEngine;

namespace Loot
{
    /// <summary>
    /// 몬스터 한 종류가 사망 시 드롭하는 골드/장비 데이터 에셋.
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

        [SerializeField]
        private EquipmentDropEntry[] equipmentDrops;

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

        /// <summary>
        /// 장비 드롭 테이블. 각 항목이 독립적으로 확률 판정된다.
        /// </summary>
        public EquipmentDropEntry[] EquipmentDrops => equipmentDrops;
    }
}
