using Core;
using Rank;
using Rank.Events;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 지정된 Button 하나를 requiredRank 미만인 동안 누를 수 없게 만드는 범용 랭크 게이트.
    /// SoldierPanelGateUI(패널 전체를 잠그는 버전)와 같은 RankService.IsAtLeast를 쓰지만,
    /// 버튼 하나만 잠그면 되는 자리(예: 스탯창의 병사 서브탭)를 위한 더 가벼운 컴포넌트다.
    /// requiredRank가 null이면 항상 눌림(조건 없음).
    ///
    /// button 자체는 잠긴 동안 interactable=false라 Unity가 onClick 자체를 아예 호출하지 않는다
    /// (비활성 Button은 클릭 이벤트를 받지 않음) — 그래서 "왜 안 눌리는지" 피드백을 주려면 별도
    /// 클릭 수신기가 필요하다. lockedOverlayButton(같은 자리를 덮는 투명 Button, 잠긴 동안만
    /// 활성화)이 그 역할을 한다 - StatRowUI의 LockOverlay(section AN, raycastTarget+draw order로
    /// 아래 버튼을 가로막는 오버레이)와 같은 발상.
    /// </summary>
    public sealed class RankGatedButtonUI : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private RankSO requiredRank;

        [SerializeField]
        private Button lockedOverlayButton;

        private void Awake()
        {
            if (lockedOverlayButton != null)
            {
                lockedOverlayButton.onClick.AddListener(OnLockedClicked);
            }
        }

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

        private void OnLockedClicked()
        {
            if (requiredRank == null)
            {
                return;
            }

            string message = requiredRank.RequiredStage != null
                ? $"스테이지 {requiredRank.RequiredStage.Chapter}-{requiredRank.RequiredStage.StageNumber} 클리어 시 이용 가능합니다."
                : $"{requiredRank.DisplayName} 랭크 달성 후 이용 가능합니다.";

            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent(message));
        }

        private void Refresh()
        {
            bool unlocked = requiredRank == null
                || (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService) && rankService.IsAtLeast(requiredRank));

            button.interactable = unlocked;

            if (lockedOverlayButton != null)
            {
                lockedOverlayButton.gameObject.SetActive(!unlocked);
            }
        }
    }
}
