using Stage;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 골드 던전 한 판의 규칙을 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "GoldDungeonConfig", menuName = "Idle Project/Dungeon/Gold Dungeon Config")]
    public sealed class GoldDungeonConfigSO : ScriptableObject
    {
        [SerializeField]
        private GameObject monsterPrefab;

        [SerializeField]
        private int monsterCount = 50;

        [SerializeField]
        private float timeLimitSeconds = 60f;

        [SerializeField]
        private int goldPerKillPerStage = 10000;

        [SerializeField]
        private float spawnViewportMargin = 0.08f;

        [SerializeField]
        private float extraStrengthMultiplier = 1f;

        [SerializeField]
        private StageSO climaxStage;

        [SerializeField]
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private StageDifficultyConfigSO difficultyConfig;

        /// <summary>
        /// 스폰할 던전 몬스터 프리팹.
        /// </summary>
        public GameObject MonsterPrefab => monsterPrefab;

        /// <summary>
        /// 한 판에 스폰되는 몬스터 수.
        /// </summary>
        public int MonsterCount => monsterCount;

        /// <summary>
        /// 제한시간(초).
        /// </summary>
        public float TimeLimitSeconds => timeLimitSeconds;

        /// <summary>
        /// 몬스터 1마리 처치당 지급 골드 = goldPerKillPerStage × 선택한 단계.
        /// </summary>
        public int GoldPerKillPerStage => goldPerKillPerStage;

        /// <summary>
        /// 화면 가장자리로부터의 스폰 제외 여백(뷰포트 비율, 0~0.5).
        /// </summary>
        public float SpawnViewportMargin => spawnViewportMargin;

        /// <summary>
        /// 스토리에서 이 몬스터를 만났을 때의 스탯 배율(climaxStage 기준)에 extraStrengthMultiplier와
        /// 선택한 단계를 곱해, 골드 던전에서도 단계를 올릴수록 몬스터가 실제로 강해지도록 계산한다.
        /// StoneDungeonConfigSO/SkillDungeonConfigSO의 CalculateBossStatMultiplier와 동일한 형태.
        /// </summary>
        public float CalculateMonsterStatMultiplier(int stageNumber)
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
