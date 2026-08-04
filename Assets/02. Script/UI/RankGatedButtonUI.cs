using Core;
using Rank;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 지정된 Button 하나를 requiredRank 미만인 동안 누를 수 없게 만드는 범용 랭크 게이트.
    /// SoldierPanelGateUI(패널 전체를 잠그는 버전)와 같은 RankService.IsAtLeast를 쓰지만,
    /// 버튼 하나만 잠그면 되는 자리(예: 스탯창의 병사 서브탭)를 위한 더 가벼운 컴포넌트다.
    /// requiredRank가 null이면 항상 눌림(조건 없음).
    /// </summary>
    public sealed class RankGatedButtonUI : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private RankSO requiredRank;

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

            button.interactable = unlocked;
        }
    }
}
