using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// StoneDungeonClearedEvent를 구독해 강화석 던전 클리어 결과(기준 스테이지/소요시간/획득
    /// 강화석)를 팝업으로 보여준다. GoldDungeonClearPopupUI와 동일한 형태.
    /// </summary>
    public sealed class StoneDungeonClearPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text summaryText;

        [SerializeField]
        private Button confirmButton;

        private void Awake()
        {
            popupRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StoneDungeonClearedEvent>(OnStoneDungeonCleared);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonClearedEvent>(OnStoneDungeonCleared);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnStoneDungeonCleared(StoneDungeonClearedEvent evt)
        {
            int minutes = Mathf.FloorToInt(evt.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(evt.ElapsedSeconds % 60f);

            summaryText.text =
                "강화석 던전 클리어!\n" +
                $"기준 스테이지: {evt.Chapter}-{evt.StageNumber}\n" +
                $"소요시간: {minutes}분 {seconds}초\n" +
                $"획득 강화석: {evt.TotalStonesEarned:N0}";

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
