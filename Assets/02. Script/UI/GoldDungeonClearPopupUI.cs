using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// GoldDungeonClearedEvent를 구독해 골드 던전 클리어 결과(기준 스테이지/소요시간/획득 골드)를
    /// 팝업으로 보여준다. 확인 버튼을 누르면 닫힌다. OfflineProgressPopupUI와 같은 "이벤트 구독 →
    /// 요약 텍스트 채우고 열기 → 확인 버튼으로 닫기" 형태.
    /// </summary>
    public sealed class GoldDungeonClearPopupUI : MonoBehaviour
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
            GameBootstrapper.Events?.Subscribe<GoldDungeonClearedEvent>(OnGoldDungeonCleared);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldDungeonClearedEvent>(OnGoldDungeonCleared);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnGoldDungeonCleared(GoldDungeonClearedEvent evt)
        {
            int minutes = Mathf.FloorToInt(evt.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(evt.ElapsedSeconds % 60f);

            summaryText.text =
                "골드 던전 클리어!\n" +
                $"기준 스테이지: {evt.Chapter}-{evt.StageNumber}\n" +
                $"소요시간: {minutes}분 {seconds}초\n" +
                $"획득 골드: {evt.TotalGoldEarned:N0}";

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
