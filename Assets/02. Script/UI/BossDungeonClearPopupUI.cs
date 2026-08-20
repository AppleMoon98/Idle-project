using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// BossDungeonClearedEvent를 구독해 보스 던전 클리어 결과(처치한 보스/소요시간/획득 증표)를
    /// 팝업으로 보여준다. UI.StoneDungeonClearPopupUI와 동일한 형태.
    /// </summary>
    public sealed class BossDungeonClearPopupUI : MonoBehaviour
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
            GameBootstrapper.Events?.Subscribe<BossDungeonClearedEvent>(OnBossDungeonCleared);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<BossDungeonClearedEvent>(OnBossDungeonCleared);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnBossDungeonCleared(BossDungeonClearedEvent evt)
        {
            int minutes = Mathf.FloorToInt(evt.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(evt.ElapsedSeconds % 60f);

            summaryText.text =
                "보스 던전 클리어!\n" +
                $"처치한 보스: {evt.BossDisplayName}\n" +
                $"소요시간: {minutes}분 {seconds}초\n" +
                $"획득 증표: {evt.TotalTokensEarned:N0}";

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
