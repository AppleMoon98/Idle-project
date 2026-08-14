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
    }
}
