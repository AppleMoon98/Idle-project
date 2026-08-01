using System;
using Soldier;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 가챠 확률 테이블 내 병사 하나의 가중치를 정의한다. GachaTableSO에서 사용한다.
    /// EquipmentDropEntry(항목별 독립 확률)와 달리, 가챠는 한 번에 정확히 하나를 뽑는
    /// 가중치 기반 선택이므로 확률(0~1)이 아니라 상대적 가중치(정수)로 표현한다.
    /// </summary>
    [Serializable]
    public sealed class GachaPoolEntry
    {
        [SerializeField]
        private SoldierSO soldier;

        [SerializeField]
        private int weight;

        /// <summary>
        /// 뽑힐 수 있는 병사.
        /// </summary>
        public SoldierSO Soldier => soldier;

        /// <summary>
        /// 다른 항목 대비 상대적 가중치. 전체 가중치 합 대비 이 값의 비율이 뽑힐 확률이 된다.
        /// </summary>
        public int Weight => weight;
    }
}
