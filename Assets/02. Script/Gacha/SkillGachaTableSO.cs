using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 스킬 가챠 한 판의 확률 테이블과 주문서 소모량을 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillGachaTable", menuName = "Idle Project/Gacha/Skill Gacha Table")]
    public sealed class SkillGachaTableSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private SkillGachaPoolEntry[] entries;

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
        public SkillGachaPoolEntry[] Entries => entries;

        /// <summary>
        /// 1회 뽑기에 소모되는 스킬 주문서 수량. CurrencyType이 Ticket일 때만 쓰인다.
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
