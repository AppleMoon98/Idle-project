using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 병사 구출 던전 진행 중 남은 시간과 구역 점령 평균 진행도를 보여준다. StoneDungeonHudUI와
    /// 같은 형태에 SoldierRescueDungeonProgressChangedEvent 구독만 추가됐다.
    /// </summary>
    public sealed class SoldierRescueDungeonHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text timeText;

        [SerializeField]
        private Text progressText;

        private float _remainingTime;
        private bool _isActive;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Subscribe<SoldierRescueDungeonProgressChangedEvent>(OnProgressChanged);

            TickerRegistration.Register(this);

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Unsubscribe<SoldierRescueDungeonProgressChangedEvent>(OnProgressChanged);

            TickerRegistration.Unregister(this);
        }

        private void OnAttemptStarted(SoldierRescueDungeonAttemptStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            UpdateTimeText();
            UpdateProgressText(0f);
        }

        private void OnAttemptFailed(SoldierRescueDungeonAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
        }

        private void OnSessionEnded(SoldierRescueDungeonSessionEndedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
        }

        private void OnProgressChanged(SoldierRescueDungeonProgressChangedEvent evt)
        {
            UpdateProgressText(evt.Progress01);
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

        private void UpdateProgressText(float progress01)
        {
            progressText.text = $"점령 진행도: {Mathf.RoundToInt(progress01 * 100f)}%";
        }
    }
}
