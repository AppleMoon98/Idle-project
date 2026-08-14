using Loot;
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
        private MonsterLootSO baseLoot;

        [SerializeField]
        private float goldMultiplierBonus = 15f;

        [SerializeField]
        private float spawnViewportMargin = 0.08f;

        [SerializeField]
        private float extraStrengthMultiplier = 1f;

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
        /// 몬스터 1마리 처치당 골드를 굴릴 때 쓰는 기준 드롭 데이터(일반 몬스터 기준,
        /// MonsterLoot_Basic) - 실제 지급액은 이 min~max에 CalculateGoldMultiplier의 배율을
        /// 곱해 Loot.LootRoller.RollGold로 굴린다(GoldDungeonSessionController가 호출).
        /// </summary>
        public MonsterLootSO BaseLoot => baseLoot;

        /// <summary>
        /// 화면 가장자리로부터의 스폰 제외 여백(뷰포트 비율, 0~0.5).
        /// </summary>
        public float SpawnViewportMargin => spawnViewportMargin;

        /// <summary>
        /// 선택한 단계(tier)를 "플레이어의 실제 역대 최고 클리어 스테이지에서 (maxStageNumber - tier)
        /// 칸 뒤" 스테이지로 해석한다(예: 최고 클리어가 2-20이고 maxStageNumber=2일 때 tier=1 →
        /// 2-19, tier=2(=maxStageNumber) → 2-20 그 자체). 예전엔 "챕터 N의 고정 -20 스테이지"였는데,
        /// 실제 진행도에 맞춰 체력/보상이 매끄럽게 이어지도록 바꿨다. highestClearedIndex/
        /// maxStageNumber는 이 에셋이 모르는 런타임 진행도라 호출하는 쪽(GoldDungeonSessionController,
        /// RankService.HighestClearedIndex와 자신의 MaxStageNumber를 가져옴)이 넘겨줘야 한다.
        /// 카탈로그 범위를 벗어나면 양 끝으로 클램프한다(방어적 처리).
        /// </summary>
        public StageSO GetReferenceStage(int tier, int highestClearedIndex, int maxStageNumber)
        {
            if (stageCatalog == null || stageCatalog.Stages == null || stageCatalog.Stages.Length == 0)
            {
                return null;
            }

            int referenceIndex = highestClearedIndex - (maxStageNumber - tier);
            referenceIndex = Mathf.Clamp(referenceIndex, 0, stageCatalog.Stages.Length - 1);
            return stageCatalog.GetAt(referenceIndex);
        }

        /// <summary>
        /// GetReferenceStage가 가리키는 스테이지의 난이도 배율에 extraStrengthMultiplier(기본 1.2,
        /// "스토리보다 1.2배 강하게")를 곱한다.
        /// </summary>
        public float CalculateMonsterStatMultiplier(int tier, int highestClearedIndex, int maxStageNumber)
        {
            if (stageCatalog == null || difficultyConfig == null)
            {
                return extraStrengthMultiplier;
            }

            StageSO referenceStage = GetReferenceStage(tier, highestClearedIndex, maxStageNumber);

            if (referenceStage == null)
            {
                return extraStrengthMultiplier;
            }

            int stageIndex = stageCatalog.IndexOf(referenceStage);
            float storyMultiplier = difficultyConfig.GetMultiplier(stageIndex);

            return storyMultiplier * extraStrengthMultiplier;
        }

        /// <summary>
        /// GetReferenceStage가 가리키는 스테이지의 골드 배율(StageDifficultyConfigSO.
        /// GetGoldMultiplierWithFloor - 더 낮은 인덱스 스테이지보다 배율이 떨어지지 않도록 보장하는
        /// 안전장치 버전)에 goldMultiplierBonus(기본 15, "그 스테이지의 15배")를 곱한다.
        /// </summary>
        public float CalculateGoldMultiplier(int tier, int highestClearedIndex, int maxStageNumber)
        {
            if (stageCatalog == null || difficultyConfig == null)
            {
                return goldMultiplierBonus;
            }

            StageSO referenceStage = GetReferenceStage(tier, highestClearedIndex, maxStageNumber);

            if (referenceStage == null)
            {
                return goldMultiplierBonus;
            }

            int stageIndex = stageCatalog.IndexOf(referenceStage);
            float stageGoldMultiplier = difficultyConfig.GetGoldMultiplierWithFloor(stageIndex);

            return stageGoldMultiplier * goldMultiplierBonus;
        }
    }
}
