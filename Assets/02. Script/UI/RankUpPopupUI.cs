using Core;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// RankPromotionAnnouncementReadyEvent를 구독해 알림 팝업을 띄운다. 이 이벤트는
    /// Rank.RankPromotionStoryGate가 RankChangedEvent(IsRestore == false)를 가로채, 그 랭크의
    /// 승급 스토리(있다면)를 먼저 재생한 뒤에만 발행한다 - 세이브 복원(IsRestore == true) 이벤트나
    /// 스토리 재생 중에는 이 이벤트 자체가 오지 않으므로, 이 컴포넌트가 따로 IsRestore를 확인하거나
    /// 스토리와의 순서를 신경 쓸 필요가 없다. 확인 버튼을 누르면 닫힌다.
    /// </summary>
    public sealed class RankUpPopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private Button confirmButton;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankPromotionAnnouncementReadyEvent>(OnRankPromotionAnnouncementReady);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAnnouncementReadyEvent>(OnRankPromotionAnnouncementReady);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnRankPromotionAnnouncementReady(RankPromotionAnnouncementReadyEvent evt)
        {
            messageText.text = $"랭크 승급!\n{evt.NewRank.DisplayName}";
            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
