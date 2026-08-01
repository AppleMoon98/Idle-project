using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 병사 가챠 한 판의 확률 테이블과 소환권 소모량을 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "GachaTable", menuName = "Idle Project/Gacha/Gacha Table")]
    public sealed class GachaTableSO : ScriptableObject
    {
        [SerializeField]
        private GachaPoolEntry[] entries;

        [SerializeField]
        private int ticketCostPerPull;

        /// <summary>
        /// 이 테이블의 확률 항목 목록.
        /// </summary>
        public GachaPoolEntry[] Entries => entries;

        /// <summary>
        /// 1회 뽑기에 소모되는 병사 소환권 수량.
        /// </summary>
        public int TicketCostPerPull => ticketCostPerPull;
    }
}
