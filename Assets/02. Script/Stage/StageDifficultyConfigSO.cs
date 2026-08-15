using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 인덱스에 따라 몬스터 스탯 배율을 계산하는 공식을 담은 데이터 에셋.
    /// 스테이지마다 개별 배율 값을 두지 않고, 카탈로그 인덱스 기반 선형 공식 하나로 전체를 관리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDifficultyConfig", menuName = "Idle Project/Stage/Stage Difficulty Config")]
    public sealed class StageDifficultyConfigSO : ScriptableObject
    {
        [SerializeField]
        private float baseMultiplier = 1f;

        [SerializeField]
        private float multiplierPerStageIndex;

        [SerializeField]
        private float goldBaseMultiplier = 1f;

        [SerializeField]
        private float goldMultiplierPerStageIndex = 0.08f;

        /// <summary>
        /// 구간별로 스탯 배율 기울기가 달라지는 계단식 목록. 비어있으면(기본값) 기존
        /// multiplierPerStageIndex 하나로 처음부터 끝까지 선형 계산한다(하위 호환). 채워져
        /// 있으면 baseMultiplier + 구간별 누적으로 계산하고, multiplierPerStageIndex는 쓰이지
        /// 않는다 - Enhancement.EnhancementConfigSO.CostIncrementTiers와 같은 "빈 배열 = 레거시
        /// 방식" 관례.
        /// </summary>
        [SerializeField]
        private StageDifficultyTier[] statMultiplierTiers = System.Array.Empty<StageDifficultyTier>();

        /// <summary>
        /// stageIndex(0부터 시작하는 카탈로그 인덱스)에 대한 스탯 배율을 계산한다.
        /// </summary>
        public float GetMultiplier(int stageIndex)
        {
            int clampedIndex = Mathf.Max(stageIndex, 0);

            return statMultiplierTiers != null && statMultiplierTiers.Length > 0
                ? CalculateTieredMultiplier(clampedIndex)
                : baseMultiplier + multiplierPerStageIndex * clampedIndex;
        }

        /// <summary>
        /// Enhancement.EnhancementService.CalculateTieredCost와 동일한 형태의 구간별 누적 계산.
        /// 각 구간은 자기 시작 인덱스부터 다음 구간 시작 인덱스(또는 마지막 구간이면 끝)까지
        /// 걸쳐있는 인덱스 수만큼만 기여한다.
        /// </summary>
        private float CalculateTieredMultiplier(int stageIndex)
        {
            float total = baseMultiplier;

            for (int i = 0; i < statMultiplierTiers.Length; i++)
            {
                int tierStart = statMultiplierTiers[i].StageIndexThreshold;
                int tierEnd = i + 1 < statMultiplierTiers.Length ? statMultiplierTiers[i + 1].StageIndexThreshold : int.MaxValue;
                int indicesInTier = Mathf.Max(0, Mathf.Min(stageIndex, tierEnd) - tierStart);

                total += indicesInTier * statMultiplierTiers[i].MultiplierPerIndex;
            }

            return total;
        }

        /// <summary>
        /// stageIndex(0부터 시작하는 카탈로그 인덱스)에 대한 골드 드롭 배율을 계산한다. GetMultiplier와
        /// 동일한 선형 공식이지만 몬스터 스탯과는 별도의 기울기를 쓴다 - 스테이지가 진행될수록
        /// 몬스터는 더 강해지고, 그만큼(다만 별도로 조절 가능한 비율로) 골드 드롭도 함께 늘어난다.
        /// </summary>
        public float GetGoldMultiplier(int stageIndex)
        {
            int clampedIndex = Mathf.Max(stageIndex, 0);
            return goldBaseMultiplier + goldMultiplierPerStageIndex * clampedIndex;
        }
    }
}
