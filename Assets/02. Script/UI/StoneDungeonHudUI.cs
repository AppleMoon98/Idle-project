using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 강화석 던전 보스전 중 남은 시간을 mm:ss로 보여준다. 실패 화면이 뜨거나 세션이
    /// 끝나면 숨긴다.
    /// </summary>
    public sealed class StoneDungeonHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text timeText;

        private float _remainingTime;
        private bool _isActive;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StoneDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<StoneDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<StoneDungeonSessionEndedEvent>(OnSessionEnded);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonSessionEndedEvent>(OnSessionEnded);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnAttemptStarted(StoneDungeonAttemptStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            UpdateTimeText();
        }

        private void OnAttemptFailed(StoneDungeonAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
        }

        private void OnSessionEnded(StoneDungeonSessionEndedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);
            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            int totalSeconds = Mathf.CeilToInt(_remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText.text = $"제한시간: {minutes:00}:{seconds:00}";
        }
    }
}
