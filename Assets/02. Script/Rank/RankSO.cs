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

        /// <summary>
        /// 화면에 표시할 랭크 이름.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 랭크가 되기 위해 클리어해야 하는 스테이지. null이면 조건 없음(시작 랭크).
        /// </summary>
        public StageSO RequiredStage => requiredStage;
    }
}
