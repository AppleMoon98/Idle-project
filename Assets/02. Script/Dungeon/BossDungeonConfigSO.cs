using Rank;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 보스 던전(승급전 보스 재도전) 한 판의 규칙을 정의하는 데이터 에셋. 단계 스테퍼가 없다 —
    /// 어떤 보스를 고를지만 선택하고, 난이도는 그 보스 자신의 기본 스탯에 extraStrengthMultiplier만
    /// 곱해 정해진다(챕터 기준 스테이지를 조회하는 Stone/Gold 던전과 달리 참조할 "챕터"가 없다 —
    /// 승급전 보스는 챕터 진행과 무관하게 랭크 하나에 고정된 콘텐츠이므로).
    /// </summary>
    [CreateAssetMenu(fileName = "BossDungeonConfig", menuName = "Idle Project/Dungeon/Boss Dungeon Config")]
    public sealed class BossDungeonConfigSO : ScriptableObject
    {
        [SerializeField]
        private RankCatalogSO rankCatalog;

        [SerializeField]
        private float timeLimitSeconds = 300f;

        [SerializeField]
        private int tokensPerClear = 10;

        [SerializeField]
        private float extraStrengthMultiplier = 1.5f;

        /// <summary>
        /// 승급전 보스 목록을 순서대로 나열한 랭크 카탈로그 — Dungeon.BossDungeonSessionController가
        /// 이 안에서 "실제 승급전 보스를 가진(Rank.Boss.PromotionBossController 보유) + 플레이어가
        /// 이미 그 랭크 이상인" 항목만 골라 선택 가능한 보스 목록을 만든다.
        /// </summary>
        public RankCatalogSO RankCatalog => rankCatalog;

        /// <summary>
        /// 제한시간(초).
        /// </summary>
        public float TimeLimitSeconds => timeLimitSeconds;

        /// <summary>
        /// 클리어 시 지급하는 보스 토벌 증표(고정값 — 단계 배율 없음).
        /// </summary>
        public int TokensPerClear => tokensPerClear;

        /// <summary>
        /// 승급전 보스 자신의 기본 스탯 대비 배율("승급전보다 N배 강하게").
        /// </summary>
        public float ExtraStrengthMultiplier => extraStrengthMultiplier;

    }
}
