using Core;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 텍스트를 잠깐 보여준 뒤 서서히 페이드아웃되며 사라지는 안내 메시지. Canvas에 전역으로 하나만
    /// 두고 ToastMessageRequestedEvent를 구독해 어떤 도메인/UI에서든 이벤트만 발행하면 뜨도록
    /// 만들어졌다 — 도메인마다 팝업 안에 이 컴포넌트를 따로 심고 씬 와이어링을 반복할 필요가 없다.
    /// Show(string)도 public으로 남겨 직접 참조를 들고 있는 호출부(드문 경우)도 계속 지원한다.
    /// WarClimaxWarningUI/CountdownTimer가 이미 쓰는 "GameTicker 기반 ITickable + CountdownTimer.Tick"
    /// 패턴을 그대로 재사용한다.
    /// </summary>
    public sealed class TemporaryMessageUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private float displaySeconds = 1.5f;

        [SerializeField]
        private float fadeOutSeconds = 0.5f;

        private float _remainingDisplay;
        private float _remainingFade;
        private bool _isDisplaying;
        private bool _isFading;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<ToastMessageRequestedEvent>(OnToastRequested);
            TickerRegistration.Register(this);

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            _isDisplaying = false;
            _isFading = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<ToastMessageRequestedEvent>(OnToastRequested);
            TickerRegistration.Unregister(this);
        }

        private void OnToastRequested(ToastMessageRequestedEvent evt)
        {
            Show(evt.Message);
        }

        /// <summary>
        /// 메시지를 즉시 표시한다. 이미 표시/페이드 중이었다면 알파를 1로 되돌리고 처음부터 다시
        /// 카운트다운한다.
        /// </summary>
        public void Show(string message)
        {
            messageText.text = message;
            canvasGroup.alpha = 1f;
            _remainingDisplay = displaySeconds;
            _isDisplaying = true;
            _isFading = false;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isDisplaying)
            {
                _remainingDisplay = CountdownTimer.Tick(_remainingDisplay, deltaTime);

                if (_remainingDisplay <= 0f)
                {
                    _isDisplaying = false;
                    _isFading = true;
                    _remainingFade = fadeOutSeconds;
                }

                return;
            }

            if (!_isFading)
            {
                return;
            }

            _remainingFade = CountdownTimer.Tick(_remainingFade, deltaTime);
            canvasGroup.alpha = fadeOutSeconds > 0f ? _remainingFade / fadeOutSeconds : 0f;

            if (_remainingFade <= 0f)
            {
                _isFading = false;
                canvasGroup.alpha = 0f;
            }
        }
    }
}
