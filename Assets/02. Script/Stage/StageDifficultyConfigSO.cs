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
        /// stageIndex(0부터 시작하는 카탈로그 인덱스)에 대한 스탯 배율을 계산한다.
        /// </summary>
        public float GetMultiplier(int stageIndex)
        {
            int clampedIndex = Mathf.Max(stageIndex, 0);
            return baseMultiplier + multiplierPerStageIndex * clampedIndex;
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

        /// <summary>
        /// GetGoldMultiplier(stageIndex)와 같지만, 0부터 stageIndex까지 중 가장 높았던 배율을
        /// 대신 반환한다 - 현재 공식은 선형 증가라 사실상 GetGoldMultiplier(stageIndex)와 동일한
        /// 값이지만, 이 공식이 나중에 챕터별로 비선형/역전 가능한 값으로 바뀌더라도 "더 낮은 인덱스
        /// 스테이지 기준일 때보다 보상이 줄어드는" 일이 없도록 보장한다(골드 던전처럼 "실제 최고
        /// 클리어 스테이지에서 몇 칸 뒤"를 기준 스테이지로 삼는 소비자를 위한 안전장치).
        /// </summary>
        public float GetGoldMultiplierWithFloor(int stageIndex)
        {
            int clampedIndex = Mathf.Max(stageIndex, 0);
            float best = goldBaseMultiplier;

            for (int i = 0; i <= clampedIndex; i++)
            {
                float candidate = goldBaseMultiplier + goldMultiplierPerStageIndex * i;

                if (candidate > best)
                {
                    best = candidate;
                }
            }

            return best;
        }
    }
}
