using Core;
using Rank;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 탭 패널의 랭크 게이트. 현재 랭크가 requiredRank 미만이면 로스터/배치 목록 대신
    /// "N-M 스테이지 클리어 후 오픈됩니다" 안내만 보여준다. 조건 문구는 requiredRank.RequiredStage를
    /// 그대로 읽어 구성하므로, 나중에 어떤 랭크/스테이지로 조건이 바뀌어도 코드 변경 없이 자동으로
    /// 반영된다. 랭크 승급 이벤트를 구독해, 패널이 열려 있는 동안 조건을 채우면 즉시 갱신된다.
    /// </summary>
    public sealed class SoldierPanelGateUI : MonoBehaviour
    {
        [SerializeField]
        private RankSO requiredRank;

        [SerializeField]
        private GameObject lockedView;

        [SerializeField]
        private Text lockedMessageText;

        [SerializeField]
        private GameObject[] unlockedContent;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool unlocked = requiredRank == null
                || (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService) && rankService.IsAtLeast(requiredRank));

            lockedView.SetActive(!unlocked);

            foreach (GameObject content in unlockedContent)
            {
                content.SetActive(unlocked);
            }

            if (!unlocked)
            {
                lockedMessageText.text = requiredRank.RequiredStage != null
                    ? $"{requiredRank.RequiredStage.Chapter}-{requiredRank.RequiredStage.StageNumber} 스테이지 클리어 후 오픈됩니다."
                    : $"{requiredRank.DisplayName} 랭크 달성 후 오픈됩니다.";
            }
        }
    }
}
