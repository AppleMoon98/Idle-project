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
        /// 방식" 관례. chapterScalingStartIndex 이후 구간은 이 목록이 아니라 챕터 수치 기반으로
        /// 계산하므로(아래 CalculateChapterScaledContribution), 목록의 마지막 구간은
        /// chapterScalingStartIndex에서 끝나는 것으로 취급한다(예전의 "마지막 구간은 무한히
        /// 이어진다" 동작 대체).
        /// </summary>
        [SerializeField]
        private StageDifficultyTier[] statMultiplierTiers = System.Array.Empty<StageDifficultyTier>();

        /// <summary>
        /// 이 인덱스 이후부터는 statMultiplierTiers 대신 챕터 번호 자체를 그 챕터 구간의 기울기로
        /// 쓴다(챕터 2는 인덱스 1칸당 +2, 챕터 3은 +3, ...) — 챕터가 늘어날수록 그 챕터 자체의
        /// 기울기도 함께 커져, 새 챕터가 추가될 때마다 별도 구간을 수동으로 늘리지 않아도 자동으로
        /// 더 가팔라진다. 기본값 39(1-40의 인덱스) — 챕터 2(인덱스 40)부터 적용된다.
        /// </summary>
        [SerializeField]
        private int chapterScalingStartIndex = 39;

        /// <summary>
        /// 챕터 하나가 차지하는 스테이지 수(카탈로그가 챕터 1~N을 각각 40개씩 순서대로 나열하는
        /// 고정 구조라는 전제 — StageDifficultyTier의 threshold 39=1-40과 동일한 전제를 공유한다).
        /// </summary>
        [SerializeField]
        private int stagesPerChapter = 40;

        /// <summary>
        /// stageIndex(0부터 시작하는 카탈로그 인덱스)에 대한 스탯 배율을 계산한다.
        /// </summary>
        public float GetMultiplier(int stageIndex)
        {
            int clampedIndex = Mathf.Max(stageIndex, 0);

            return statMultiplierTiers != null && statMultiplierTiers.Length > 0
                ? CalculateTieredMultiplier(clampedIndex) + CalculateChapterScaledContribution(clampedIndex)
                : baseMultiplier + multiplierPerStageIndex * clampedIndex;
        }

        /// <summary>
        /// Enhancement.EnhancementService.CalculateTieredCost와 동일한 형태의 구간별 누적 계산.
        /// 각 구간은 자기 시작 인덱스부터 다음 구간 시작 인덱스(또는 마지막 구간이면
        /// chapterScalingStartIndex)까지 걸쳐있는 인덱스 수만큼만 기여한다.
        /// </summary>
        private float CalculateTieredMultiplier(int stageIndex)
        {
            int cappedIndex = Mathf.Min(stageIndex, chapterScalingStartIndex);
            float total = baseMultiplier;

            for (int i = 0; i < statMultiplierTiers.Length; i++)
            {
                int tierStart = statMultiplierTiers[i].StageIndexThreshold;
                int tierEnd = i + 1 < statMultiplierTiers.Length ? statMultiplierTiers[i + 1].StageIndexThreshold : chapterScalingStartIndex;
                int indicesInTier = Mathf.Max(0, Mathf.Min(cappedIndex, tierEnd) - tierStart);

                total += indicesInTier * statMultiplierTiers[i].MultiplierPerIndex;
            }

            return total;
        }

        /// <summary>
        /// chapterScalingStartIndex를 넘어선 인덱스에 대해, 인덱스 1칸씩 그 칸이 속한 챕터 번호만큼
        /// 누적한다(챕터 2 구간의 40칸은 전부 +2, 챕터 3 구간의 40칸은 전부 +3, ...). 매 호출마다
        /// 처음부터 다시 더하므로(캐싱 없음) 챕터 수가 아주 많아지면(수백 챕터) 느려질 수 있지만,
        /// 몬스터 스폰 시점에만 호출되는 계산이라 실질적인 문제는 없다.
        /// </summary>
        private float CalculateChapterScaledContribution(int stageIndex)
        {
            if (stageIndex <= chapterScalingStartIndex || stagesPerChapter <= 0)
            {
                return 0f;
            }

            float total = 0f;

            for (int i = chapterScalingStartIndex + 1; i <= stageIndex; i++)
            {
                int chapter = i / stagesPerChapter + 1;
                total += chapter;
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
