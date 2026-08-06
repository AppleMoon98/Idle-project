using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 던전 보스전 중 남은 시간을 mm:ss로 보여준다. 실패 화면이 뜨거나 세션이 끝나면 숨긴다.
    /// StoneDungeonHudUI와 동일한 형태.
    /// </summary>
    public sealed class SkillDungeonHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text timeText;

        private float _remainingTime;
        private bool _isActive;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<SkillDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<SkillDungeonSessionEndedEvent>(OnSessionEnded);

            TickerRegistration.Register(this);

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonSessionEndedEvent>(OnSessionEnded);

            TickerRegistration.Unregister(this);
        }

        private void OnAttemptStarted(SkillDungeonAttemptStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            UpdateTimeText();
        }

        private void OnAttemptFailed(SkillDungeonAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
        }

        private void OnSessionEnded(SkillDungeonSessionEndedEvent evt)
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

            _remainingTime = CountdownTimer.Tick(_remainingTime, deltaTime);
            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            timeText.text = $"제한시간: {CountdownTimer.FormatMinutesSeconds(_remainingTime)}";
        }
    }
}
