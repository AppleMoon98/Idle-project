using Stage;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 강화석 던전(보스전) 한 판의 규칙을 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "StoneDungeonConfig", menuName = "Idle Project/Dungeon/Stone Dungeon Config")]
    public sealed class StoneDungeonConfigSO : ScriptableObject
    {
        [SerializeField]
        private GameObject bossPrefab;

        [SerializeField]
        private float timeLimitSeconds = 300f;

        [SerializeField]
        private int stonesPerClearPerStage = 50;

        [SerializeField]
        private float extraStrengthMultiplier = 1.5f;

        [SerializeField]
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private StageDifficultyConfigSO difficultyConfig;

        /// <summary>
        /// 선택한 단계 N을 "챕터 N의 N-40 스테이지"(챕터 클라이맥스 보스 스테이지) 몬스터 체력
        /// 기준으로 해석할 때 쓰는 고정 스테이지 번호. GoldDungeonConfigSO의 ReferenceStageNumber(20)와
        /// 같은 패턴이며, 강화석 던전은 "챕터 보스"를 기준으로 삼는다는 의미로 40을 쓴다.
        /// </summary>
        private const int ReferenceStageNumber = 40;

        /// <summary>
        /// 스폰할 보스 프리팹(War 시스템의 보스를 그대로 재사용).
        /// </summary>
        public GameObject BossPrefab => bossPrefab;

        /// <summary>
        /// 제한시간(초).
        /// </summary>
        public float TimeLimitSeconds => timeLimitSeconds;

        /// <summary>
        /// 클리어 시 지급하는 강화석 = stonesPerClearPerStage × 선택한 단계.
        /// </summary>
        public int StonesPerClearPerStage => stonesPerClearPerStage;

        /// <summary>
        /// 선택한 단계 N을 "챕터 N의 N-40 스테이지"(챕터 클라이맥스 보스) 몬스터 체력 기준으로
        /// 해석한다(예: 1단계 → 1-40, 2단계 → 2-40). N 자체가 유효한 범위인지(콘텐츠 존재 여부,
        /// 플레이어 진행도)는 호출하는 쪽(StoneDungeonSessionController)이 미리 클램프해서 넘겨야
        /// 한다 — 이 데이터 에셋은 런타임 서비스(랭크/진행도)를 모르므로 순수하게 "주어진 단계를
        /// 스테이지로 해석"만 담당한다. 카탈로그에 해당 챕터가 없으면 존재하는 마지막 챕터의 -40
        /// 스테이지로 대체한다(방어적 처리). 여기서 구한 스테이지의 난이도 배율에
        /// extraStrengthMultiplier(기본 1.5, "그 챕터 보스보다 50% 강하게")만 곱해 최종 배율을 낸다.
        /// </summary>
        public float CalculateBossStatMultiplier(int stageNumber)
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
        /// 선택한 단계 N의 기준 스테이지(챕터 N의 -40 스테이지)를 반환한다. 존재하지 않으면 카탈로그에
        /// 실제로 존재하는 가장 높은 챕터의 -40 스테이지로 대체한다(콘텐츠가 줄어도 항상 유효한 기준
        /// 스테이지를 반환하기 위함). 공개 API인 이유: 입장 조건 판정(해당 스테이지를 실제로
        /// 클리어했는지)을 위해 StoneDungeonSessionController가 이 스테이지 자체를 필요로 한다.
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
