using Stage;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 스킬 던전(보스전) 한 판의 규칙을 정의하는 데이터 에셋. StoneDungeonConfigSO와 동일한
    /// 형태 — 보상만 강화석 대신 스킬 주문서다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDungeonConfig", menuName = "Idle Project/Dungeon/Skill Dungeon Config")]
    public sealed class SkillDungeonConfigSO : ScriptableObject
    {
        [SerializeField]
        private GameObject bossPrefab;

        [SerializeField]
        private float timeLimitSeconds = 300f;

        [SerializeField]
        private int scrollsPerClearPerStage = 1;

        [SerializeField]
        private float extraStrengthMultiplier = 1.5f;

        [SerializeField]
        private StageSO climaxStage;

        [SerializeField]
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private StageDifficultyConfigSO difficultyConfig;

        [SerializeField]
        private float spawnViewportMargin = 0.2f;

        /// <summary>
        /// 스폰할 보스 프리팹(War 시스템의 보스를 그대로 재사용).
        /// </summary>
        public GameObject BossPrefab => bossPrefab;

        /// <summary>
        /// 제한시간(초).
        /// </summary>
        public float TimeLimitSeconds => timeLimitSeconds;

        /// <summary>
        /// 클리어 시 지급하는 스킬 주문서 = scrollsPerClearPerStage × 선택한 단계.
        /// </summary>
        public int ScrollsPerClearPerStage => scrollsPerClearPerStage;

        /// <summary>
        /// 화면 가장자리로부터의 스폰 제외 여백(뷰포트 비율, 0~0.5).
        /// </summary>
        public float SpawnViewportMargin => spawnViewportMargin;

        /// <summary>
        /// 스토리에서 이 보스를 만났을 때의 스탯 배율(climaxStage 기준)에 extraStrengthMultiplier와
        /// 선택한 단계를 곱해, 스킬 던전에서는 항상 스토리보다 강하게 등장하도록 계산한다.
        /// </summary>
        public float CalculateBossStatMultiplier(int stageNumber)
        {
            float storyMultiplier = 1f;

            if (climaxStage != null && stageCatalog != null && difficultyConfig != null)
            {
                int stageIndex = stageCatalog.IndexOf(climaxStage);
                storyMultiplier = difficultyConfig.GetMultiplier(stageIndex);
            }

            return storyMultiplier * extraStrengthMultiplier * Mathf.Max(1, stageNumber);
        }
    }
}
