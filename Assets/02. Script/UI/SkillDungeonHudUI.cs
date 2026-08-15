using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 던전 보스전 중 남은 시간을 mm:ss로, 그 아래에 보스 전용 체력바를 보여준다. 실패
    /// 화면이 뜨거나 세션이 끝나면 숨긴다. StoneDungeonHudUI와 동일한 형태.
    /// </summary>
    public sealed class SkillDungeonHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text timeText;

        [SerializeField]
        private Image bossHealthFillImage;

        [SerializeField]
        private float bossHealthFillTweenDuration = 0.15f;

        private float _remainingTime;
        private bool _isActive;
        private GameObject _bossInstance;
        private float _targetBossFillAmount;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<SkillDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<SkillDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Register(this);

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<SkillDungeonSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Unregister(this);
        }

        private void OnAttemptStarted(SkillDungeonAttemptStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            UpdateTimeText();

            _bossInstance = evt.BossInstance;
            RefreshBossHealthImmediate();
        }

        private void OnAttemptFailed(SkillDungeonAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
            _bossInstance = null;
        }

        private void OnSessionEnded(SkillDungeonSessionEndedEvent evt)
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
        /// StoneDungeonHudUI.OnBossHealthChanged와 동일한 형태 — 초기값은
        /// RefreshBossHealthImmediate가 직접 읽어 채운다(Health.SetCurrent는 값이 실제로 바뀔 때만
        /// 이벤트를 발행하므로, 스폰 직후 이미 최대 체력이면 이 이벤트가 한 번도 안 올 수 있다).
        /// </summary>
        private void OnBossHealthChanged(CharacterHealthChangedEvent evt)
        {
            if (_bossInstance == null || evt.Character != _bossInstance)
            {
                return;
            }

            _targetBossFillAmount = evt.Max > 0f ? evt.Current / evt.Max : 0f;
        }

        private void RefreshBossHealthImmediate()
        {
            float fill = 0f;

            if (_bossInstance != null
                && _bossInstance.TryGetComponent(out Health bossHealth)
                && _bossInstance.TryGetComponent(out CharacterStatsProvider statsProvider)
                && statsProvider.Stats.MaxHealth > 0f)
            {
                fill = bossHealth.Current / statsProvider.Stats.MaxHealth;
            }

            _targetBossFillAmount = fill;
            bossHealthFillImage.fillAmount = fill;
        }
    }
}
