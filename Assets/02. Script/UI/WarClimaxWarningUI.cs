using Core;
using UnityEngine;
using UnityEngine.UI;
using War.Events;

namespace UI
{
    /// <summary>
    /// 클라이맥스 스테이지 진입 직후 워밍업(경고 카운트다운) 동안 표시되는 팝업. WarBattleHudUI와
    /// 마찬가지로 War 도메인 컴포넌트를 직접 참조하지 않고 이벤트로만 반응한다. 실제 목표 판정이
    /// 시작되는 WarClimaxStateChangedEvent가 오면 IsClimax 값과 무관하게(워밍업 종료든 클라이맥스
    /// 이탈이든) 무조건 숨긴다.
    /// </summary>
    public sealed class WarClimaxWarningUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject warningRoot;

        [SerializeField]
        private Text objectiveBannerText;

        [SerializeField]
        private Text countdownText;

        private float _remaining;
        private bool _isCountingDown;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<WarClimaxWarmupStartedEvent>(OnWarmupStarted);
            GameBootstrapper.Events?.Subscribe<WarClimaxStateChangedEvent>(OnClimaxStateChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            warningRoot.SetActive(false);
            _isCountingDown = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<WarClimaxWarmupStartedEvent>(OnWarmupStarted);
            GameBootstrapper.Events?.Unsubscribe<WarClimaxStateChangedEvent>(OnClimaxStateChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnWarmupStarted(WarClimaxWarmupStartedEvent evt)
        {
            objectiveBannerText.text = WarObjectiveBannerText.Resolve(evt.ObjectiveType);
            _remaining = evt.Duration;
            _isCountingDown = true;
            warningRoot.SetActive(true);
            UpdateCountdownText();
        }

        private void OnClimaxStateChanged(WarClimaxStateChangedEvent evt)
        {
            _isCountingDown = false;
            warningRoot.SetActive(false);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isCountingDown)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - deltaTime);
            UpdateCountdownText();
        }

        private void UpdateCountdownText()
        {
            countdownText.text = Mathf.CeilToInt(_remaining).ToString();
        }
    }
}
