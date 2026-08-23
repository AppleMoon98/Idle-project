using Stage;
using UnityEngine;

namespace Rank
{
    /// <summary>
    /// 랭크 하나(예: "시골 소년", "병사")를 정의하는 데이터 에셋. RequiredStage를 클리어하면
    /// 이 랭크가 된다. 시작 랭크는 RequiredStage가 null(조건 없이 처음부터 이 랭크).
    /// </summary>
    [CreateAssetMenu(fileName = "Rank", menuName = "Idle Project/Rank/Rank")]
    public sealed class RankSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private StageSO requiredStage;

        [SerializeField]
        private int maxDeployableSquads;

        [SerializeField]
        private int maxDeploymentCost;

        [SerializeField]
        private GameObject bossPrefab;

        [SerializeField]
        private GameObject bossDungeonPrefab;

        [SerializeField]
        private float playerStatBonusPercent;

        /// <summary>
        /// 화면에 표시할 랭크 이름.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 랭크가 되기 위해 클리어해야 하는 스테이지. null이면 조건 없음(시작 랭크).
        /// RequiredStage를 클리어했다고 바로 승급하지는 않는다 - 조건을 만족하면 "랭크 승급 가능"
        /// 버튼이 뜰 뿐이고, 실제 승급은 BossPrefab과의 전투에서 이겨야 확정된다.
        /// </summary>
        public StageSO RequiredStage => requiredStage;

        /// <summary>
        /// 이 랭크에서 완전히 꾸릴 수 있는 부대 수(Soldier.SoldierDeploymentService.SquadCount 이하).
        /// 예: 1이면 1부대(20슬롯)까지 전부 배치 가능, 2면 1~2부대(40슬롯)까지 가능. 랭크가
        /// 오를수록 늘어난다. 실제 슬롯 수 환산은 SoldierDeploymentService.GetMaxUnlockedSlotCount가
        /// 담당한다(이 값 × SlotsPerSquad).
        /// </summary>
        public int MaxDeployableSquads => maxDeployableSquads;

        /// <summary>
        /// 이 랭크에서 배치 가능한 전체 코스트 예산(Soldier.SoldierDeploymentService.TryDeploy가
        /// 소모하는, Soldier.SoldierSO.Cost 합의 상한). 랭크가 오를수록 늘어난다 - 예: 시골 소년
        /// 10, 병사 20, 십인 대장 30.
        /// </summary>
        public int MaxDeploymentCost => maxDeploymentCost;

        /// <summary>
        /// 이 랭크로 승급하기 위해 처치해야 하는 보스. null이면(콘텐츠 미비) 조건을 만족해도
        /// 승급 가능 버튼이 뜨지 않는다(RankService.IsNextRankAvailable 참고).
        /// </summary>
        public GameObject BossPrefab => bossPrefab;

        /// <summary>
        /// 보스 토벌 던전(Dungeon.BossDungeonSessionController)이 승급전 대신 스폰할 별도
        /// 프리팹. null이면(기본값, 콘텐츠 미비 랭크 포함) BossPrefab을 그대로 재사용한다 - 승급전과
        /// 보스 토벌의 패턴/스탯이 서로 다르게 튜닝돼야 하는 랭크만 이 필드를 채우면 된다
        /// (RankSO.RequiredStage == null과 같은 sparse opt-in 관례). "병사" 랭크는 승급전
        /// (Monster_Boss_Rank1Promotion, HP5000/ATK100)과 보스 토벌(Monster_BossDungeon_Rank1,
        /// HP40000/ATK500 + 별도 패턴)이 완전히 독립된 프리팹/데이터를 쓴다 - 하나를 공유하던 시절
        /// 보스 토벌 쪽만 조정하려던 패턴 변경이 승급전에도 그대로 새어 들어간 문제가 있었다.
        /// </summary>
        public GameObject BossDungeonPrefab => bossDungeonPrefab;

        /// <summary>
        /// 이 랭크에서 플레이어 자신의 공격력/체력에 적용되는 보너스 비율(기본 스탯 대비, 예:
        /// 0.5 = +50%). 병사(부대) 스탯에는 영향을 주지 않는다 - Character.RankStatReceiver가
        /// Player 오브젝트에만 부착돼 적용하며, Soldier 쪽은 완전히 별개인
        /// SoldierEnhancementService/SoldierStatReceiver 트랙을 그대로 쓴다. 기본값 0(보너스 없음).
        /// </summary>
        public float PlayerStatBonusPercent => playerStatBonusPercent;
    }
}
