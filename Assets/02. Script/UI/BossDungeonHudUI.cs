using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 보스 던전 전투 중 남은 시간을 mm:ss로, 그 아래에 보스 전용 체력바를 보여준다. 실패 화면이
    /// 뜨거나 세션이 끝나면 숨긴다. UI.StoneDungeonHudUI와 완전히 동일한 형태.
    /// </summary>
    public sealed class BossDungeonHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text timeText;

        [SerializeField]
        private Image bossHealthFillImage;

        [SerializeField]
        private Text bossHealthText;

        [SerializeField]
        private float bossHealthFillTweenDuration = 0.15f;

        private float _remainingTime;
        private bool _isActive;
        private GameObject _bossInstance;
        private float _targetBossFillAmount;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<BossDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<BossDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<BossDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Register(this);

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<BossDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<BossDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<BossDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Unregister(this);
        }

        private void OnAttemptStarted(BossDungeonAttemptStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            UpdateTimeText();

            _bossInstance = evt.BossInstance;
            RefreshBossHealthImmediate();
        }

        private void OnAttemptFailed(BossDungeonAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
            _bossInstance = null;
        }

        private void OnSessionEnded(BossDungeonSessionEndedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
            _bossInstance = null;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _remainingTime = CountdownTimer.Tick(_remainingTime, deltaTime);
            UpdateTimeText();

            if (!Mathf.Approximately(bossHealthFillImage.fillAmount, _targetBossFillAmount))
            {
                float step = deltaTime / bossHealthFillTweenDuration;
                bossHealthFillImage.fillAmount = Mathf.MoveTowards(bossHealthFillImage.fillAmount, _targetBossFillAmount, step);
            }
        }

        private void UpdateTimeText()
        {
            timeText.text = $"제한시간: {CountdownTimer.FormatMinutesSeconds(_remainingTime)}";
        }

        private void OnBossHealthChanged(CharacterHealthChangedEvent evt)
        {
            if (_bossInstance == null || evt.Character != _bossInstance)
            {
                return;
            }

            _targetBossFillAmount = evt.Max > 0f ? evt.Current / evt.Max : 0f;
            UpdateBossHealthText(evt.Current, evt.Max);
        }

        private void RefreshBossHealthImmediate()
        {
            float fill = 0f;
            float current = 0f;
            float max = 0f;

            if (_bossInstance != null
                && _bossInstance.TryGetComponent(out Health bossHealth)
                && _bossInstance.TryGetComponent(out CharacterStatsProvider statsProvider)
                && statsProvider.Stats.MaxHealth > 0f)
            {
                current = bossHealth.Current;
                max = statsProvider.Stats.MaxHealth;
                fill = current / max;
            }

            _targetBossFillAmount = fill;
            bossHealthFillImage.fillAmount = fill;
            UpdateBossHealthText(current, max);
        }

        private void UpdateBossHealthText(float current, float max)
        {
            bossHealthText.text = $"{current:N0} / {max:N0}";
        }
    }
}
