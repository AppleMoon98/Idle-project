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
        private string displayName;

        [SerializeField]
        private GachaPoolEntry[] entries;

        [SerializeField]
        private int ticketCostPerPull;

        [SerializeField]
        private GachaCurrencyType currencyType = GachaCurrencyType.Ticket;

        [SerializeField]
        private int goldCostPerPull;

        /// <summary>
        /// 이 티어의 표시 이름(가챠 팝업 하위 탭 라벨용, 예: "일반 뽑기").
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 테이블의 확률 항목 목록.
        /// </summary>
        public GachaPoolEntry[] Entries => entries;

        /// <summary>
        /// 1회 뽑기에 소모되는 병사 소환권 수량. CurrencyType이 Ticket일 때만 쓰인다.
        /// </summary>
        public int TicketCostPerPull => ticketCostPerPull;

        /// <summary>
        /// 이 티어가 소모하는 재화 종류.
        /// </summary>
        public GachaCurrencyType CurrencyType => currencyType;

        /// <summary>
        /// 1회 뽑기에 소모되는 골드. CurrencyType이 Gold일 때만 쓰인다.
        /// </summary>
        public int GoldCostPerPull => goldCostPerPull;
    }
}
