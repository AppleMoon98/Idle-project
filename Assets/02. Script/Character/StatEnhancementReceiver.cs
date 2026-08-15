using Core;
using Enhancement;
using Enhancement.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// StatEnhancedEvent를 구독해 이 캐릭터의 RuntimeStats에 강화 증가량을 적용한다.
    /// Enhancement 도메인은 이 컴포넌트나 "Player"라는 개념을 전혀 모른다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class StatEnhancementReceiver : MonoBehaviour
    {
        private CharacterStatsProvider _statsProvider;
        private Health _health;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StatEnhancedEvent>(OnStatEnhanced);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StatEnhancedEvent>(OnStatEnhanced);
        }

        private void OnStatEnhanced(StatEnhancedEvent evt)
        {
            RuntimeStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, evt.StatType, evt.ValuePerLevel);

            if (evt.StatType == EnhancementStatType.MaxHealth)
            {
                _health.NotifyMaxHealthChanged();
            }
        }
    }
}
