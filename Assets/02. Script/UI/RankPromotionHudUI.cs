using Character;
using Character.Events;
using Core;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 랭크 승급전 전투 중 상단에 보스 전용 체력바를 보여준다. UI.BossDungeonHudUI/
    /// StoneDungeonHudUI와 동일한 형태이지만, 승급전은 제한시간이 없어(Rank.
    /// RankPromotionBattleController 문서 참고) 카운트다운 대신 "{대상 랭크} 승급전"
    /// 타이틀을 보여준다. 실패 화면이 뜨거나 세션이 끝나면 숨긴다.
    /// </summary>
    public sealed class RankPromotionHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Image bossHealthFillImage;

        [SerializeField]
        private Text bossHealthText;

        [SerializeField]
        private float bossHealthFillTweenDuration = 0.15f;

        private bool _isActive;
        private GameObject _bossInstance;
        private float _targetBossFillAmount;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Subscribe<RankPromotionAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Subscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Register(this);

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAttemptStartedEvent>(OnAttemptStarted);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionAttemptFailedEvent>(OnAttemptFailed);
            GameBootstrapper.Events?.Unsubscribe<RankPromotionSessionEndedEvent>(OnSessionEnded);
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnBossHealthChanged);

            TickerRegistration.Unregister(this);
        }

        private void OnAttemptStarted(RankPromotionAttemptStartedEvent evt)
        {
            _isActive = true;
            hudRoot.SetActive(true);

            titleText.text = evt.TargetRank != null ? $"{evt.TargetRank.DisplayName} 승급전" : "승급전";

            _bossInstance = evt.BossInstance;
            RefreshBossHealthImmediate();
        }

        private void OnAttemptFailed(RankPromotionAttemptFailedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
            _bossInstance = null;
        }

        private void OnSessionEnded(RankPromotionSessionEndedEvent evt)
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

            if (!Mathf.Approximately(bossHealthFillImage.fillAmount, _targetBossFillAmount))
            {
                float step = deltaTime / bossHealthFillTweenDuration;
                bossHealthFillImage.fillAmount = Mathf.MoveTowards(bossHealthFillImage.fillAmount, _targetBossFillAmount, step);
            }
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
