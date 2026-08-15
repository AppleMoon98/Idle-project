using System.Collections.Generic;
using Core;
using Enhancement;
using Equipment.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// EquipmentPossessionStatsChangedEvent(장비 보유 효과 - 장착 여부 무관)를 구독해 이 캐릭터의
    /// RuntimeStats에 보너스를 적용한다. EquipmentStatReceiver(장착 시 효과)와 같은 "누적 총합을
    /// 받아 직전에 적용해둔 값과의 차이만큼만 반영" 구조지만, 실제 적용은 RuntimeStatApplier가
    /// 아니라 PossessionStatApplier를 쓴다(AttackPower/MaxHealth까지 %로 취급하기 위함).
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class EquipmentPossessionStatReceiver : MonoBehaviour
    {
        private CharacterStatsProvider _statsProvider;
        private Health _health;
        private readonly Dictionary<EnhancementStatType, float> _applied = new();

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<EquipmentPossessionStatsChangedEvent>(OnPossessionStatsChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<EquipmentPossessionStatsChangedEvent>(OnPossessionStatsChanged);
        }

        private void OnPossessionStatsChanged(EquipmentPossessionStatsChangedEvent evt)
        {
            _applied.TryGetValue(evt.StatType, out float previousTotal);
            float delta = evt.NewTotalPercent - previousTotal;
            _applied[evt.StatType] = evt.NewTotalPercent;

            PossessionStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, evt.StatType, delta);

            if (evt.StatType == EnhancementStatType.MaxHealth)
            {
                _health.NotifyMaxHealthChanged();
            }
        }
    }
}
