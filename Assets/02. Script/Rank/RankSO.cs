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
        private int maxDeployableSoldiers = 2;

        [SerializeField]
        private GameObject bossPrefab;

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
        /// 이 랭크에서 동시에 배치할 수 있는 병사 슬롯 수(최대 30). 랭크가 오를수록 늘어난다.
        /// </summary>
        public int MaxDeployableSoldiers => maxDeployableSoldiers;

        /// <summary>
        /// 이 랭크로 승급하기 위해 처치해야 하는 보스. null이면(콘텐츠 미비) 조건을 만족해도
        /// 승급 가능 버튼이 뜨지 않는다(RankService.IsNextRankAvailable 참고).
        /// </summary>
        public GameObject BossPrefab => bossPrefab;
    }
}
