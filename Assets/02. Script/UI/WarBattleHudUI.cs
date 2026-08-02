using Core;
using UnityEngine;
using UnityEngine.UI;
using War;
using War.Events;

namespace UI
{
    /// <summary>
    /// War 클라이맥스 스테이지 진행 상황을 표시하는 HUD. WarClimaxStateChangedEvent /
    /// WarObjectiveProgressChangedEvent만 구독하며 War 도메인 컴포넌트를 직접 참조하지 않는다.
    /// 전멸/보스 처치는 각각 StageInfoUI(남은 몬스터 수)/HealthBarUI(보스 체력바)가 이미
    /// 같은 정보를 보여주므로 진행 게이지는 구조물 점령/수하물 보호에서만 표시한다.
    /// </summary>
    public sealed class WarBattleHudUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text objectiveBannerText;

        [SerializeField]
        private GameObject progressGaugeRoot;

        [SerializeField]
        private Image progressFillImage;

        [SerializeField]
        private Text progressPercentText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<WarClimaxStateChangedEvent>(OnClimaxStateChanged);
            GameBootstrapper.Events?.Subscribe<WarObjectiveProgressChangedEvent>(OnObjectiveProgressChanged);

            hudRoot.SetActive(false);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<WarClimaxStateChangedEvent>(OnClimaxStateChanged);
            GameBootstrapper.Events?.Unsubscribe<WarObjectiveProgressChangedEvent>(OnObjectiveProgressChanged);
        }

        private void OnClimaxStateChanged(WarClimaxStateChangedEvent evt)
        {
            hudRoot.SetActive(evt.IsClimax);

            if (!evt.IsClimax)
            {
                return;
            }

            objectiveBannerText.text = WarObjectiveBannerText.Resolve(evt.ObjectiveType);

            bool showsGauge = evt.ObjectiveType == WarObjectiveType.StructureCapture
                || evt.ObjectiveType == WarObjectiveType.CargoProtection;

            progressGaugeRoot.SetActive(showsGauge);
            progressFillImage.fillAmount = 0f;
            progressPercentText.text = "0%";
        }

        private void OnObjectiveProgressChanged(WarObjectiveProgressChangedEvent evt)
        {
            progressFillImage.fillAmount = evt.Progress01;
            progressPercentText.text = $"{Mathf.RoundToInt(evt.Progress01 * 100f)}%";
        }
    }
}
