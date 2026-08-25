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
    /// 구독해 플레이어와 같은 비율의 효과를 받는다. 델타/잔여시간을 (스탯 타입, 발행한 스킬)
    /// 복합 키로 추적한다 - 스탯 타입만으로 키를 잡으면 서로 다른 두 스킬이 같은 스탯(예: 공속)을
    /// 동시에 버프할 때 나중 것이 먼저 것을 되돌려버려(교체) 곱연산으로 쌓이지 않는 문제가 있었다.
    /// SkillSO를 키에 포함시키면: 같은 스킬이 재시전되면(같은 키) 자기 자신의 이전 적용분만
    /// 정확히 되돌리고 새로 적용해(무한 중첩 방지, 자동 발동 스킬이 쿨다운마다 재시전돼도 안전),
    /// 다른 스킬이 같은 스탯을 버프하면(다른 키) 서로의 델타를 건드리지 않아 SkillBuffStatApplier가
    /// "현재 값" 기준으로 계산하는 특성상 자연스럽게 곱연산으로 중첩된다(예: -10% 다음 -15%를
    /// 걸면 최종은 base*0.9*0.85, 단순 합산인 -25%가 아니다).
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class SoldierStatReceiver : MonoBehaviour, ITickable
    {
        private readonly struct BuffKey : System.IEquatable<BuffKey>
        {
            public readonly EnhancementStatType StatType;
            public readonly SkillSO Source;

            public BuffKey(EnhancementStatType statType, SkillSO source)
            {
                StatType = statType;
                Source = source;
            }

            public bool Equals(BuffKey other) => StatType == other.StatType && Source == other.Source;
            public override bool Equals(object obj) => obj is BuffKey other && Equals(other);
            public override int GetHashCode() => System.HashCode.Combine(StatType, Source);
        }

        private CharacterStatsProvider _statsProvider;
        private Health _health;

        private readonly Dictionary<BuffKey, float> _buffDeltas = new();
        private readonly Dictionary<BuffKey, float> _buffRemaining = new();
        private readonly List<BuffKey> _expiredBuffScratch = new();

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

            if (evt.StatType == EnhancementStatType.MaxHealth)
            {
                _health.NotifyMaxHealthChanged();
            }
        }

        private void OnSkillSelfBuffApplied(SkillSelfBuffAppliedEvent evt)
        {
            var key = new BuffKey(evt.StatType, evt.Source);
            RevertBuff(key);

            float delta = SkillBuffStatApplier.ApplyPercent(_statsProvider.Stats, evt.StatType, evt.Percent);
            _buffDeltas[key] = delta;
            _buffRemaining[key] = evt.Duration;
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

                List<BuffKey> keys = new(_buffRemaining.Keys);
                foreach (BuffKey key in keys)
                {
                    float remaining = _buffRemaining[key] - deltaTime;
                    if (remaining <= 0f)
                    {
                        _expiredBuffScratch.Add(key);
                    }
                    else
                    {
                        _buffRemaining[key] = remaining;
                    }
                }

                foreach (BuffKey key in _expiredBuffScratch)
                {
                    RevertBuff(key);
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

        private void RevertBuff(BuffKey key)
        {
            if (_buffDeltas.TryGetValue(key, out float delta))
            {
                SkillBuffStatApplier.Revert(_statsProvider.Stats, key.StatType, delta);
                _buffDeltas.Remove(key);
            }

            _buffRemaining.Remove(key);
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
