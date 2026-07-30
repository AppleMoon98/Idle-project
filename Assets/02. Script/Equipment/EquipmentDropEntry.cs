using System;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 드롭 테이블 내 장비 하나의 드롭 확률을 정의한다. StageSO의 장비 드롭 테이블에서 사용한다.
    /// </summary>
    [Serializable]
    public sealed class EquipmentDropEntry
    {
        [SerializeField]
        private EquipmentSO equipment;

        [SerializeField]
        [Range(0f, 1f)]
        private float dropChance;

        /// <summary>
        /// 드롭될 장비.
        /// </summary>
        public EquipmentSO Equipment => equipment;

        /// <summary>
        /// 이 장비가 드롭될 확률 (0~1).
        /// </summary>
        public float DropChance => dropChance;
    }
}
