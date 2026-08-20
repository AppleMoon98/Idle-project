using Stage;
using War;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 병사 구출 던전(구역 점령전) 한 판의 규칙을 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "SoldierRescueDungeonConfig", menuName = "Idle Project/Dungeon/Soldier Rescue Dungeon Config")]
    public sealed class SoldierRescueDungeonConfigSO : ScriptableObject
    {
        [SerializeField]
        private WarStructureSO structureDefinition;

        [SerializeField]
        private int zoneCount = 3;

        [SerializeField]
        private float minDistanceBetweenZones = 4f;

        [SerializeField]
        private float timeLimitSeconds = 180f;

        [SerializeField]
        private int ticketsPerClearPerStage = 5;

        [SerializeField]
        private StageCatalogSO stageCatalog;

        /// <summary>
        /// StoneDungeonConfigSO와 동일한 패턴 — 선택한 단계 N을 "챕터 N의 N-40 스테이지"(챕터
        /// 클라이맥스) 기준으로 해석한다.
        /// </summary>
        private const int ReferenceStageNumber = 40;

        /// <summary>
        /// 점령 구역(WarStructure) 프리팹에 물릴 데이터 정의. War 시스템의 기존 정의를 그대로 재사용한다.
        /// </summary>
        public WarStructureSO StructureDefinition => structureDefinition;

        /// <summary>
        /// 동시에 생성되는 점령 구역 수.
        /// </summary>
        public int ZoneCount => zoneCount;

        /// <summary>
        /// 구역끼리 유지해야 하는 최소 거리.
        /// </summary>
        public float MinDistanceBetweenZones => minDistanceBetweenZones;

        /// <summary>
        /// 제한시간(초).
        /// </summary>
        public float TimeLimitSeconds => timeLimitSeconds;

        /// <summary>
        /// 클리어 시 지급하는 병사 뽑기 재료 = ticketsPerClearPerStage × 선택한 단계.
        /// </summary>
        public int TicketsPerClearPerStage => ticketsPerClearPerStage;

        /// <summary>
        /// 선택한 단계 N의 기준 스테이지(챕터 N의 -40 스테이지)를 반환한다. 존재하지 않으면
        /// 카탈로그에 실제로 존재하는 가장 높은 챕터의 -40 스테이지로 대체한다.
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
