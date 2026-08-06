using System;
using Equipment;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 장비 뽑기 슬롯 하나(무기/장갑/갑옷/투구/신발)가 갖는 티어 배열을 묶는다.
    /// Stage.MonsterSpawnEntry와 같은 "플레인 직렬화 래퍼" 형태 — EquipmentGachaService가
    /// 슬롯별로 별도의 확률 테이블 세트를 갖도록 하기 위함이다.
    /// </summary>
    [Serializable]
    public sealed class EquipmentGachaSlotTiers
    {
        [SerializeField]
        private EquipmentType slot;

        [SerializeField]
        private EquipmentGachaTableSO[] tiers;

        /// <summary>
        /// 이 항목이 대표하는 장비 슬롯.
        /// </summary>
        public EquipmentType Slot => slot;

        /// <summary>
        /// 이 슬롯의 티어 목록(일반/고급/유료 등).
        /// </summary>
        public EquipmentGachaTableSO[] Tiers => tiers;
    }
}
