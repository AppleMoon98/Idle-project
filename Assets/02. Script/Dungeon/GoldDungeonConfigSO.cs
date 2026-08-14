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
        private float spawnViewportMargin = 0.08f;

        [SerializeField]
        private float extraStrengthMultiplier = 1f;

        [SerializeField]
        private float goldMultiplierBonus = 15f;

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
        /// 몬스터 1마리 처치당 지급할 골드의 최소~최대 범위 - "GetReferenceStage가 가리키는
        /// 스테이지를 실제로 클리어했을 때 얻는 총 골드"에 goldMultiplierBonus(기본 15, "그
        /// 총합의 15배")를 곱한 값을 기본으로 삼되, 0번부터 그 스테이지까지 전체를 훑어 그중
        /// 가장 높았던 값으로 대체한다.
        ///
        /// 골드 배율(StageDifficultyConfigSO.GetGoldMultiplier)은 인덱스에 선형 비례해 항상
        /// 증가하지만, 스테이지의 몬스터 마릿수·구성(Stage.StageGoldRangeCalculator가 합산하는
        /// SpawnEntries/TacticEntries)은 스테이지마다 크게 다르다 - 특히 챕터 클라이맥스(N-40)는
        /// 방패벽 대형 등으로 마릿수가 훨씬 많아, 배율만 보면 더 나중 스테이지인데도 실제 총
        /// 골드는 더 낮게 나오는 역행이 실제로 발생했다(2-10이 1-40보다 카탈로그상 뒤인데도 총
        /// 골드는 더 낮았음, 실사용 중 발견). 배율에만 안전장치(GetGoldMultiplierWithFloor)를
        /// 두는 것으로는 부족해서, 최종 결과(배율까지 곱한 뒤) 자체를 0..stageIndex 구간
        /// 전체에서 훑어 min/max 각각 가장 높았던 값으로 대체한다 - 실질적으로는 각 챕터의 N-40이
        /// 지역 최댓값이 되는 경우가 대부분이라, 전체를 훑어도 비교 대상은 몇 개의 N-40 스테이지로
        /// 좁혀지는 셈이다.
        /// </summary>
        public void CalculateGoldRange(int tier, int highestClearedIndex, int maxStageNumber, out int min, out int max)
        {
            min = 0;
            max = 0;

            if (stageCatalog == null || difficultyConfig == null || stageCatalog.Stages == null)
            {
                return;
            }

            StageSO referenceStage = GetReferenceStage(tier, highestClearedIndex, maxStageNumber);

            if (referenceStage == null)
            {
                return;
            }

            int stageIndex = stageCatalog.IndexOf(referenceStage);

            for (int i = 0; i <= stageIndex; i++)
            {
                StageSO candidateStage = stageCatalog.GetAt(i);

                if (candidateStage == null)
                {
                    continue;
                }

                float candidateMultiplier = difficultyConfig.GetGoldMultiplier(i) * goldMultiplierBonus;
                StageGoldRangeCalculator.Calculate(candidateStage, out int rawMin, out int rawMax);

                int candidateMin = Mathf.Max(1, Mathf.RoundToInt(rawMin * candidateMultiplier));
                int candidateMax = Mathf.Max(candidateMin, Mathf.RoundToInt(rawMax * candidateMultiplier));

                if (candidateMin > min)
                {
                    min = candidateMin;
                }

                if (candidateMax > max)
                {
                    max = candidateMax;
                }
            }
        }
    }
}
