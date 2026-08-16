using Rank;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 디버그/테스트 전용 버튼 - 랭크 승급 자격(RankService.IsNextRankAvailable)과 무관하게
    /// 즉시 targetRank의 승급전으로 진입시킨다. RankPromotionBattleController.Enter는 현재
    /// 랭크를 검증하지 않고 targetRank.BossPrefab만 있으면 진입을 허용하므로(section AY),
    /// 실제 승급 조건을 갖추지 않은 상태에서도 승급전 콘텐츠를 바로 테스트할 수 있다.
    /// </summary>
    public sealed class PromotionTestButtonUI : MonoBehaviour
    {
        [SerializeField]
        private Button testButton;

        [SerializeField]
        private RankPromotionBattleController battleController;

        [SerializeField]
        private RankSO targetRank;

        private void OnEnable()
        {
            testButton.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            testButton.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            battleController?.Enter(targetRank);
        }
    }
}
