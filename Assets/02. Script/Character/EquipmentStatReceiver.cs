using System.Collections.Generic;
using Core;
using Enhancement;
using Equipment.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// EquipmentStatsChangedEvent를 구독해 이 캐릭터의 RuntimeStats에 장비 보너스를 적용한다.
    /// Equipment 도메인은 이 컴포넌트나 "Player"라는 개념을 전혀 모른다.
    /// 강화(StatEnhancementReceiver)와 달리 장비는 교체/해제로 값이 줄어들 수 있으므로, 이벤트가
    /// 담은 값은 누적 총합이고 이 리시버가 직전에 적용해둔 값과의 차이만큼만 RuntimeStats에 반영한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class EquipmentStatReceiver : MonoBehaviour
    {
        private CharacterStatsProvider _statsProvider;
        private readonly Dictionary<EnhancementStatType, float> _applied = new();

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<EquipmentStatsChangedEvent>(OnEquipmentStatsChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<EquipmentStatsChangedEvent>(OnEquipmentStatsChanged);
        }

        private void OnEquipmentStatsChanged(EquipmentStatsChangedEvent evt)
        {
            _applied.TryGetValue(evt.StatType, out float previousTotal);
            float delta = evt.NewTotalBonus - previousTotal;
            _applied[evt.StatType] = evt.NewTotalBonus;

            RuntimeStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, evt.StatType, delta);
        }
    }
}
