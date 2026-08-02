using System;
using Equipment;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 무기 가챠 확률 테이블 내 장비 하나의 가중치를 정의한다. EquipmentGachaTableSO에서 사용한다.
    /// GachaPoolEntry(병사)와 완전히 대칭되는 구조 — 카테고리마다 보상 타입이 다르므로
    /// 공용 제네릭으로 묶지 않고 그대로 병렬로 둔다.
    /// </summary>
    [Serializable]
    public sealed class EquipmentGachaPoolEntry
    {
        [SerializeField]
        private EquipmentSO equipment;

        [SerializeField]
        private int weight;

        /// <summary>
        /// 뽑힐 수 있는 장비.
        /// </summary>
        public EquipmentSO Equipment => equipment;

        /// <summary>
        /// 다른 항목 대비 상대적 가중치. 전체 가중치 합 대비 이 값의 비율이 뽑힐 확률이 된다.
        /// </summary>
        public int Weight => weight;
    }
}
