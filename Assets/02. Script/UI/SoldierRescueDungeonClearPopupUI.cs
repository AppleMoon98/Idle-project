using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SoldierRescueDungeonClearedEvent를 구독해 병사 구출 던전 클리어 결과(기준 스테이지/소요시간/
    /// 획득 병사 뽑기권)를 팝업으로 보여준다. GoldDungeonClearPopupUI와 동일한 형태.
    /// </summary>
    public sealed class SoldierRescueDungeonClearPopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text summaryText;

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
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonClearedEvent>(OnSoldierRescueDungeonCleared);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonClearedEvent>(OnSoldierRescueDungeonCleared);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnSoldierRescueDungeonCleared(SoldierRescueDungeonClearedEvent evt)
        {
            int minutes = Mathf.FloorToInt(evt.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(evt.ElapsedSeconds % 60f);

            summaryText.text =
                "병사 구출 던전 클리어!\n" +
                $"기준 스테이지: {evt.Chapter}-{evt.StageNumber}\n" +
                $"소요시간: {minutes}분 {seconds}초\n" +
                $"획득 뽑기권: {evt.TotalTicketsEarned:N0}";

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
