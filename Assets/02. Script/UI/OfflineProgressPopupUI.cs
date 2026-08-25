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
        private const float SecondsPerHour = 3600f;
        private const float SecondsPerMinute = 60f;

        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text summaryText;

        [SerializeField]
        private Button confirmButton;

        /// <summary>
        /// 오프라인 인정 시간이 이 값 미만이면 팝업을 띄우지 않는다. 보상 자체는
        /// OfflineProgressService.CalculateAndApply()에서 이 팝업과 무관하게 이미 적용되어 있으므로,
        /// 여기서는 "굳이 알릴 정도는 아닌" 짧은 부재의 알림만 생략한다.
        /// </summary>
        [SerializeField]
        private float minElapsedSecondsToShowPopup = 300f;

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
            if (evt.ElapsedSeconds < minElapsedSecondsToShowPopup)
            {
                return;
            }

            summaryText.text =
                $"{FormatElapsedDuration(evt.ElapsedSeconds)} 동안 자리를 비웠습니다.\n" +
                $"골드 {KoreanNumberFormatter.Format(evt.GoldEarned)} 획득\n" +
                $"장비 {evt.EquipmentEarned.Count}개 획득\n" +
                $"몬스터 {evt.MonstersKilled}마리 처치\n" +
                $"현재 스테이지: {evt.FinalChapter}-{evt.FinalStageNumber}";

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }

        /// <summary>
        /// 1시간 미만이면 "n분 n초", 그 이상이면 기존처럼 "N시간"(소수 첫째 자리까지)으로 표기한다.
        /// </summary>
        private static string FormatElapsedDuration(float elapsedSeconds)
        {
            if (elapsedSeconds < SecondsPerHour)
            {
                int totalSeconds = Mathf.FloorToInt(elapsedSeconds);
                int minutes = totalSeconds / (int)SecondsPerMinute;
                int seconds = totalSeconds % (int)SecondsPerMinute;
                return $"{minutes}분 {seconds}초";
            }

            float elapsedHours = elapsedSeconds / SecondsPerHour;
            return $"{elapsedHours:0.#}시간";
        }
    }
}
