using Enhancement;
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
        /// 골드 뽑기 누적 횟수에 따른 비용 증가 구간. 비어있으면(기본값) goldCostPerPull 고정값
        /// 그대로(하위 호환) - Enhancement.EnhancementConfigSO.CostIncrementTiers와 같은 관례.
        /// CurrencyType이 Ticket인 테이블은 이 필드를 채우지 않는다(소환권은 던전으로 이미
        /// 게이트돼 있어 무한 뽑기 문제가 없음).
        /// </summary>
        [SerializeField]
        private CostIncrementTier[] costIncrementTiers = System.Array.Empty<CostIncrementTier>();

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
        /// 1회 뽑기에 소모되는 골드(costIncrementTiers가 비어있을 때의 고정값). CurrencyType이
        /// Gold일 때만 쓰인다. 실제 다음 1회 비용은 GetGoldCostForPull을 통해 구한다.
        /// </summary>
        public int GoldCostPerPull => goldCostPerPull;

        /// <summary>
        /// pullsSoFar(이 테이블에서 지금까지 성공한 골드 뽑기 횟수)번째 다음 1회 뽑기 비용.
        /// costIncrementTiers가 비어있으면 goldCostPerPull 고정값 그대로 반환한다. 실제 계단식
        /// 계산은 Enhancement.EnhancementService와 공유하는 CostIncrementTier.CalculateTotal이 담당한다.
        /// </summary>
        public int GetGoldCostForPull(int pullsSoFar)
        {
            long total = CostIncrementTier.CalculateTotal(goldCostPerPull, costIncrementTiers, pullsSoFar);
            return (int)Mathf.Min(total, int.MaxValue);
        }
    }
}
