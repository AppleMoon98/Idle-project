using System.Collections.Generic;
using Character;
using Core;
using Enhancement;
using Managers;
using Skill;
using Skill.Events;
using SoldierEnhancement;
using SoldierEnhancement.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 병사 강화(SoldierEnhancement)의 누적 보너스를 매 스폰마다 기준치부터 다시 적용하고,
    /// 스킬 파티 버프(SkillSelfBuffAppliedEvent)/파티 회복(SkillPartyHealAppliedEvent)을
    /// 구독해 플레이어와 같은 비율의 효과를 받는다. 여러 스킬이 서로 다른 스탯을 동시에
    /// 버프할 수 있어(예: 전장의 외침의 이속/공속 + 전투의 함성의 공격력) 스탯별로 독립된
    /// 델타/잔여시간을 Dictionary로 추적한다 - 스칼라 하나만 쓰면 나중에 건 버프가 먼저 건
    /// 버프를 덮어써 버리는 문제가 생긴다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class SoldierStatReceiver : MonoBehaviour, ITickable
    {
        private CharacterStatsProvider _statsProvider;
        private Health _health;

        private readonly Dictionary<EnhancementStatType, float> _buffDeltas = new();
        private readonly Dictionary<EnhancementStatType, float> _buffRemaining = new();
        private readonly List<EnhancementStatType> _expiredBuffScratch = new();

        private float _healPercentPerSecond;
        private float _healRemaining;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _buffDeltas.Clear();
            _buffRemaining.Clear();
            _healPercentPerSecond = 0f;
            _healRemaining = 0f;

            ApplyCumulativeFromBase();

            GameBootstrapper.Events?.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            GameBootstrapper.Events?.Subscribe<SkillSelfBuffAppliedEvent>(OnSkillSelfBuffApplied);
            GameBootstrapper.Events?.Subscribe<SkillPartyHealAppliedEvent>(OnSkillPartyHealApplied);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            GameBootstrapper.Events?.Unsubscribe<SkillSelfBuffAppliedEvent>(OnSkillSelfBuffApplied);
            GameBootstrapper.Events?.Unsubscribe<SkillPartyHealAppliedEvent>(OnSkillPartyHealApplied);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnSoldierStatEnhanced(SoldierStatEnhancedEvent evt)
        {
            RuntimeStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, evt.StatType, evt.ValuePerLevel);
        }

        private void OnSkillSelfBuffApplied(SkillSelfBuffAppliedEvent evt)
        {
            RevertBuff(evt.StatType);

            float delta = SkillBuffStatApplier.ApplyPercent(_statsProvider.Stats, evt.StatType, evt.Percent);
            _buffDeltas[evt.StatType] = delta;
            _buffRemaining[evt.StatType] = evt.Duration;
        }

        private void OnSkillPartyHealApplied(SkillPartyHealAppliedEvent evt)
        {
            _healPercentPerSecond = evt.HealPercentPerSecond;
            _healRemaining = evt.Duration;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_buffRemaining.Count > 0)
            {
                _expiredBuffScratch.Clear();

                List<EnhancementStatType> keys = new(_buffRemaining.Keys);
                foreach (EnhancementStatType statType in keys)
                {
                    float remaining = _buffRemaining[statType] - deltaTime;
                    if (remaining <= 0f)
                    {
                        _expiredBuffScratch.Add(statType);
                    }
                    else
                    {
                        _buffRemaining[statType] = remaining;
                    }
                }

                foreach (EnhancementStatType statType in _expiredBuffScratch)
                {
                    RevertBuff(statType);
                }
            }

            if (_healRemaining > 0f)
            {
                _health.Heal(_statsProvider.Stats.MaxHealth * _healPercentPerSecond * deltaTime);
                _healRemaining -= deltaTime;
                if (_healRemaining < 0f)
                {
                    _healRemaining = 0f;
                }
            }
        }

        private void RevertBuff(EnhancementStatType statType)
        {
            if (_buffDeltas.TryGetValue(statType, out float delta))
            {
                SkillBuffStatApplier.Revert(_statsProvider.Stats, statType, delta);
                _buffDeltas.Remove(statType);
            }

            _buffRemaining.Remove(statType);
        }

        private void ApplyCumulativeFromBase()
        {
            _statsProvider.Stats.ResetTo(_statsProvider.BaseStats);

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierEnhancementService service))
            {
                return;
            }

            foreach (EnhancementStatType statType in service.StatTypes)
            {
                float cumulativeDelta = service.GetValuePerLevel(statType) * service.GetLevel(statType);
                RuntimeStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, statType, cumulativeDelta);
            }

            _health.Revive();
        }
    }
}
