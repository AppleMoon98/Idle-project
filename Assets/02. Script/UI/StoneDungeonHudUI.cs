using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 강화석 던전 보스전 중 남은 시간을 mm:ss로, 그 아래에 보스 전용 체력바를 보여준다. 실패
    /// 화면이 뜨거나 세션이 끝나면 숨긴다.
    /// </summary>
    public sealed class StoneDungeonHudUI : MonoBehaviour, ITickable
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
            GameBootstrapper.Events?.Subscribe<StoneDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<StoneDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<StoneDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Register(this);

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<StoneDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Unregister(this);
        }

        private void OnAttemptStarted(StoneDungeonAttemptStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            UpdateTimeText();

            _bossInstance = evt.BossInstance;
            RefreshBossHealthImmediate();
        }

        private void OnAttemptFailed(StoneDungeonAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
            _bossInstance = null;
        }

        private void OnSessionEnded(StoneDungeonSessionEndedEvent evt)
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

        /// <summary>
        /// Character.HealthBarUI와 같은 형태로 CharacterHealthChangedEvent를 구독해 보스 체력바를
        /// 갱신한다. Health.Revive()는 값이 실제로 바뀔 때만 이벤트를 발행하므로(Health.SetCurrent),
        /// 스폰 직후 이미 최대 체력이면 이 이벤트가 한 번도 안 올 수 있다 — 초기값은
        /// RefreshBossHealthImmediate가 직접 읽어 채운다.
        /// </summary>
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
