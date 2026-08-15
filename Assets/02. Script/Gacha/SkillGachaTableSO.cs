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
        /// 이 티어의 표시 이름(가챠 팝업 하위 탭 라벨용, 예: "일반 뽑기").
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 테이블의 확률 항목 목록. catalog에 등록된 스킬 전부를 매번 새로 조립해 반환한다
        /// (스킬 수가 적어 캐싱 없이 매 호출 재생성해도 비용이 무시할 만하다) - catalog가
        /// 비어있으면 빈 배열.
        /// </summary>
        public SkillGachaPoolEntry[] Entries
        {
            get
            {
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

                return result;
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
        /// costIncrementTiers가 비어있으면 goldCostPerPull 고정값 그대로 반환한다.
        /// </summary>
        public int GetGoldCostForPull(int pullsSoFar)
        {
            if (costIncrementTiers == null || costIncrementTiers.Length == 0)
            {
                return goldCostPerPull;
            }

            long total = goldCostPerPull;

            for (int i = 0; i < costIncrementTiers.Length; i++)
            {
                int tierStart = costIncrementTiers[i].LevelThreshold;
                int tierEnd = i + 1 < costIncrementTiers.Length ? costIncrementTiers[i + 1].LevelThreshold : int.MaxValue;
                int pullsInTier = Mathf.Max(0, Mathf.Min(pullsSoFar, tierEnd) - tierStart);

                total += (long)pullsInTier * costIncrementTiers[i].Increment;
            }

            return (int)Mathf.Min(total, int.MaxValue);
        }
    }
}
