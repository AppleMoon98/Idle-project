using System;
using Enhancement;
using UnityEngine;

namespace SoldierEquipment
{
    /// <summary>
    /// 장비 하나가 제공하는 스탯 보너스 한 항목. 플레이어 Equipment와 달리 등급(Grade)/강화
    /// 배율이 없으므로, 아이템마다 이 목록으로 직접 성능을 차별화한다.
    /// </summary>
    [Serializable]
    public sealed class SoldierStatBonusEntry
    {
        [SerializeField]
        private EnhancementStatType statType;

        [SerializeField]
        private float value;

        /// <summary>
        /// 보너스를 받는 스탯 종류.
        /// </summary>
        public EnhancementStatType StatType => statType;

        /// <summary>
        /// 보너스 수치.
        /// </summary>
        public float Value => value;
    }
}
