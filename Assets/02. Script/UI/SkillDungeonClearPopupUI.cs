using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SkillDungeonClearedEvent를 구독해 스킬 던전 클리어 결과(단계/소요시간/획득 주문서)를
    /// 팝업으로 보여준다. GoldDungeonClearPopupUI와 동일한 형태 — 다만 SkillDungeonConfigSO에는
    /// 챕터 기준 스테이지 개념이 없어(section BI) "기준 스테이지" 대신 "단계"만 표시한다.
    /// </summary>
    public sealed class SkillDungeonClearPopupUI : MonoBehaviour
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
            GameBootstrapper.Events?.Subscribe<SkillDungeonClearedEvent>(OnSkillDungeonCleared);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonClearedEvent>(OnSkillDungeonCleared);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnSkillDungeonCleared(SkillDungeonClearedEvent evt)
        {
            int minutes = Mathf.FloorToInt(evt.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(evt.ElapsedSeconds % 60f);

            summaryText.text =
                "스킬 던전 클리어!\n" +
                $"단계: {evt.StageNumber}층\n" +
                $"소요시간: {minutes}분 {seconds}초\n" +
                $"획득 주문서: {evt.TotalScrollsEarned:N0}";

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
