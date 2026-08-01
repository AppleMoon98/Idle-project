using Core;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// RankChangedEvent를 구독해 실제 승급(IsRestore == false)일 때만 알림 팝업을 띄운다.
    /// 앱 시작 시 세이브 복원으로 발행되는 이벤트(IsRestore == true)는 무시해, 매번 켤 때마다
    /// "승급했습니다" 알림이 뜨는 걸 막는다. 확인 버튼을 누르면 닫힌다.
    /// </summary>
    public sealed class RankUpPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private Button confirmButton;

        private void Awake()
        {
            popupRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            if (evt.IsRestore)
            {
                return;
            }

            messageText.text = $"랭크 승급!\n{evt.NewRank.DisplayName}";
            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
