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

        /// <summary>
        /// 이 티어의 표시 이름(가챠 팝업 하위 탭 라벨용, 예: "일반 뽑기").
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 테이블의 확률 항목 목록.
        /// </summary>
        public SkillGachaPoolEntry[] Entries => entries;

        /// <summary>
        /// 1회 뽑기에 소모되는 스킬 주문서 수량.
        /// </summary>
        public int TicketCostPerPull => ticketCostPerPull;
    }
}
