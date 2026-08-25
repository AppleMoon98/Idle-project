using Enhancement;
using Skill;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 스킬 가챠 한 판의 확률 테이블과 주문서 소모량을 정의하는 데이터 에셋. 항목(entries)을
    /// 인스펙터에 직접 나열하던 예전 방식은, 새 스킬이 SkillCatalogSO에 추가돼도 이 테이블을
    /// 손으로 갱신하지 않으면 뽑기 후보에서 영영 빠지는 문제가 있었다 — 이제 SkillCatalogSO를
    /// 참조만 해두면 Entries가 그때그때 카탈로그 전체 스킬로 자동 생성된다(모든 스킬 동일
    /// 가중치). 카탈로그에 새 스킬을 등록하는 것만으로 모든 스킬 가챠 테이블에 자동 반영된다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillGachaTable", menuName = "Idle Project/Gacha/Skill Gacha Table")]
    public sealed class SkillGachaTableSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private SkillCatalogSO catalog;

        [SerializeField]
        private int weightPerSkill = 1;

        [SerializeField]
        private int ticketCostPerPull;

        [SerializeField]
        private GachaCurrencyType currencyType = GachaCurrencyType.Ticket;

        [SerializeField]
        private int goldCostPerPull;

        /// <summary>
        /// 골드 뽑기 누적 횟수에 따른 비용 증가 구간. Gacha.GachaTableSO.CostIncrementTiers와
        /// 같은 관례(비어있으면 goldCostPerPull 고정값 그대로).
        /// </summary>
        [SerializeField]
        private CostIncrementTier[] costIncrementTiers = System.Array.Empty<CostIncrementTier>();

        /// <summary>
        /// Entries 최초 접근 시 한 번만 계산해두는 캐시(GitHub 이슈 #21) - catalog/weightPerSkill은
        /// 둘 다 직렬화된 SO 데이터라 런타임에 바뀌지 않으므로, 접근할 때마다 다시 조립할 이유가
        /// 없다. 300연 뽑기가 시도마다 이 프로퍼티를 읽으면서(SkillGachaService.TryPullOne) 매번
        /// 카탈로그 전체를 순회해 새 배열을 만드는 게 실측 성능 문제의 핵심 원인이었다.
        /// </summary>
        private SkillGachaPoolEntry[] _cachedEntries;

        /// <summary>
        /// 이 티어의 표시 이름(가챠 팝업 하위 탭 라벨용, 예: "일반 뽑기").
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 테이블의 확률 항목 목록. catalog에 등록된 스킬 전부로 조립되며, 최초 접근 시에만
        /// 계산하고 이후로는 캐시를 그대로 반환한다(GitHub 이슈 #21). catalog가 비어있으면 빈 배열.
        /// </summary>
        public SkillGachaPoolEntry[] Entries
        {
            get
            {
                if (_cachedEntries != null)
                {
                    return _cachedEntries;
                }

                if (catalog == null || catalog.Skills == null)
                {
                    return System.Array.Empty<SkillGachaPoolEntry>();
                }

                SkillSO[] skills = catalog.Skills;
                var result = new SkillGachaPoolEntry[skills.Length];

                for (int i = 0; i < skills.Length; i++)
                {
                    result[i] = new SkillGachaPoolEntry(skills[i], weightPerSkill);
                }

                _cachedEntries = result;
                return _cachedEntries;
            }
        }

        /// <summary>
        /// 1회 뽑기에 소모되는 스킬 주문서 수량. CurrencyType이 Ticket일 때만 쓰인다.
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
        /// 계산은 Gacha.GachaTableSO/Enhancement.EnhancementService와 공유하는
        /// CostIncrementTier.CalculateTotal이 담당한다.
        /// </summary>
        public int GetGoldCostForPull(int pullsSoFar)
        {
            long total = CostIncrementTier.CalculateTotal(goldCostPerPull, costIncrementTiers, pullsSoFar);
            return (int)Mathf.Min(total, int.MaxValue);
        }
    }
}
