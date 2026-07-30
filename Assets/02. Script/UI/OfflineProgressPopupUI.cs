using Core;
using Offline.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// OfflineProgressCalculatedEvent를 구독해 오프라인 보상 결과를 요약 팝업으로 보여준다.
    /// 확인 버튼을 누르면 닫힌다.
    /// </summary>
    public sealed class OfflineProgressPopupUI : MonoBehaviour
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
            GameBootstrapper.Events?.Subscribe<OfflineProgressCalculatedEvent>(OnOfflineProgressCalculated);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<OfflineProgressCalculatedEvent>(OnOfflineProgressCalculated);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnOfflineProgressCalculated(OfflineProgressCalculatedEvent evt)
        {
            float elapsedHours = evt.ElapsedSeconds / 3600f;

            summaryText.text =
                $"{elapsedHours:0.#}시간 동안 자리를 비웠습니다.\n" +
                $"골드 {evt.GoldEarned} 획득\n" +
                $"장비 {evt.EquipmentEarned.Count}개 획득\n" +
                $"몬스터 {evt.MonstersKilled}마리 처치\n" +
                $"현재 스테이지: {evt.FinalChapter}-{evt.FinalStageNumber}";

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
