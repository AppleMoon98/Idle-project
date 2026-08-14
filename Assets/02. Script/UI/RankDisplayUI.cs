using Core;
using Rank;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 좌측 상단(기존 골드 텍스트 자리)에 현재 랭크 이름을 표시하는 클릭 가능한 텍스트. 누르면
    /// RankInfoPopupUI를 연다. RankChangedEvent만 구독하며 Rank 도메인 로직은 전혀 갖지 않는다
    /// (RankService/RankSO의 값을 그대로 표시만 함).
    /// </summary>
    public sealed class RankDisplayUI : MonoBehaviour
    {
        [SerializeField]
        private Text rankText;

        [SerializeField]
        private Button button;

        [SerializeField]
        private RankInfoPopupUI infoPopup;

        private void Awake()
        {
            button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                Refresh(rankService.CurrentRank);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            Refresh(evt.NewRank);
        }

        private void OnClicked()
        {
            infoPopup?.Open();
        }

        private void Refresh(RankSO rank)
        {
            rankText.text = rank != null ? rank.DisplayName : string.Empty;
        }
    }
}
