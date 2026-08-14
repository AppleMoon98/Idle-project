using Core;
using Rank;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 랭크 텍스트(RankDisplayUI)를 누르면 뜨는 정보 팝업 - 현재 랭크의 플레이어 스탯 보너스와
    /// 다음 랭크/요구 스테이지를 보여준다. 순수 조회용이라 어떤 상태도 바꾸지 않는다.
    /// </summary>
    public sealed class RankInfoPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text statBonusText;

        [SerializeField]
        private Text nextRankText;

        [SerializeField]
        private Button closeButton;

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            Refresh();
            popupRoot.SetActive(true);
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                return;
            }

            RankSO current = rankService.CurrentRank;
            float percent = current != null ? current.PlayerStatBonusPercent : 0f;
            statBonusText.text = $"공격력 / 체력 +{Mathf.RoundToInt(percent * 100f)}%";

            RankSO next = rankService.GetNextRank();

            if (next == null)
            {
                nextRankText.text = "다음 랭크 : -";
            }
            else if (next.RequiredStage == null)
            {
                nextRankText.text = $"다음 랭크 : {next.DisplayName}";
            }
            else
            {
                nextRankText.text = $"다음 랭크 : {next.DisplayName} ({next.RequiredStage.Chapter}-{next.RequiredStage.StageNumber} 클리어 필요)";
            }
        }
    }
}
