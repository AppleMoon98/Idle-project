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
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private StageDifficultyConfigSO difficultyConfig;

        /// <summary>
        /// 선택한 단계 N을 "챕터 N, N-20 스테이지"의 몬스터 체력 기준으로 해석할 때 쓰는 고정 스테이지
        /// 번호. 20 자체는 "챕터 하나를 대표하는 후반 스테이지"라는 의미로 고른 값이라 상수로 둔다.
        /// </summary>
        private const int ReferenceStageNumber = 20;

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
        /// 선택한 단계 N을 "챕터 N의 N-20 스테이지" 몬스터 체력 기준으로 해석한다(예: 1단계 → 1-20,
        /// 2단계 → 2-20). N 자체가 유효한 범위인지(콘텐츠 존재 여부, 플레이어 진행도)는 호출하는 쪽
        /// (GoldDungeonSessionController)이 미리 클램프해서 넘겨야 한다 — 이 데이터 에셋은 런타임
        /// 서비스(랭크/진행도)를 모르므로 순수하게 "주어진 단계를 스테이지로 해석"만 담당한다.
        /// 카탈로그에 해당 챕터가 없으면 존재하는 마지막 챕터의 -20 스테이지로 대체한다(방어적 처리).
        /// 여기서 구한 스테이지의 난이도 배율에 extraStrengthMultiplier(기본 1.2, "스토리보다 1.2배
        /// 강하게")만 곱해 최종 배율을 낸다.
        /// </summary>
        public float CalculateMonsterStatMultiplier(int stageNumber)
        {
            if (stageCatalog == null || difficultyConfig == null)
            {
                return extraStrengthMultiplier;
            }

            StageSO referenceStage = GetReferenceStage(stageNumber);

            if (referenceStage == null)
            {
                return extraStrengthMultiplier;
            }

            int stageIndex = stageCatalog.IndexOf(referenceStage);
            float storyMultiplier = difficultyConfig.GetMultiplier(stageIndex);

            return storyMultiplier * extraStrengthMultiplier;
        }

        /// <summary>
        /// 선택한 단계 N의 기준 스테이지(챕터 N의 -20 스테이지)를 반환한다. 존재하지 않으면 카탈로그에
        /// 실제로 존재하는 가장 높은 챕터의 -20 스테이지로 대체한다(콘텐츠가 줄어도 항상 유효한 기준
        /// 스테이지를 반환하기 위함). 공개 API인 이유: 입장 조건 판정(해당 스테이지를 실제로
        /// 클리어했는지)을 위해 GoldDungeonSessionController가 이 스테이지 자체를 필요로 한다.
        /// </summary>
        public StageSO GetReferenceStage(int stageNumber)
        {
            if (stageCatalog == null)
            {
                return null;
            }

            int chapter = Mathf.Max(1, stageNumber);
            StageSO stage = stageCatalog.Find(chapter, ReferenceStageNumber);

            if (stage != null)
            {
                return stage;
            }

            return stageCatalog.Find(stageCatalog.GetMaxChapter(), ReferenceStageNumber);
        }
    }
}
