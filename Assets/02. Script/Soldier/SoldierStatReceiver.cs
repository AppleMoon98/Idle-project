using Character;
using Core;
using Enhancement;
using Managers;
using Skill.Events;
using SoldierEnhancement;
using SoldierEnhancement.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// SoldierEnhancementService의 병사 전용 강화를 이 병사 유닛의 RuntimeStats에 적용한다.
    /// Character.StatEnhancementReceiver(Player)와 달리, 병사는 PoolManager로 계속 스폰/디스폰을
    /// 반복하며 RuntimeStats(CharacterStatsProvider가 캐싱)가 풀 재사용 사이에도 그대로 남아있으므로,
    /// 매 스폰(OnEnable)마다 원본 기준으로 리셋한 뒤 현재 누적 레벨을 통째로 다시 적용해야 한다
    /// (StageMonsterScaler.ApplyScale과 동일한 이유). 이후 살아있는 동안의 실시간 강화는 이벤트로
    /// 받은 델타만 추가 적용한다.
    /// 그와 별개로 SkillSelfBuffAppliedEvent(전투의 함성 등 SelfBuff 스킬)를 구독해, 살아있는 동안
    /// 자기 자신의 현재 공격력 기준 %만큼 임시 버프를 받고 지속시간 후 스스로 되돌린다 — 강화처럼
    /// 영구 누적되는 값이 아니라 매번 revert-then-reapply하는 임시 버프라 별도의 Tick 카운트다운이
    /// 필요하다(Skill.Effects.SelfBuffSkillEffect가 시전자 자신에게 적용하는 것과 동일한 방식).
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class SoldierStatReceiver : MonoBehaviour, ITickable
    {
        private CharacterStatsProvider _statsProvider;
        private Health _health;
        private float _appliedBuffBonus;
        private float _buffRemaining;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _appliedBuffBonus = 0f;
            _buffRemaining = 0f;

            ApplyCumulativeFromBase();
            GameBootstrapper.Events?.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            GameBootstrapper.Events?.Subscribe<SkillSelfBuffAppliedEvent>(OnSkillSelfBuffApplied);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            GameBootstrapper.Events?.Unsubscribe<SkillSelfBuffAppliedEvent>(OnSkillSelfBuffApplied);

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
            RevertBuff();

            _appliedBuffBonus = _statsProvider.Stats.AttackPower * evt.AttackPowerPercent;
            _statsProvider.Stats.AttackPower += _appliedBuffBonus;
            _buffRemaining = evt.Duration;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_buffRemaining <= 0f)
            {
                return;
            }

            _buffRemaining -= deltaTime;

            if (_buffRemaining <= 0f)
            {
                RevertBuff();
            }
        }

        private void RevertBuff()
        {
            if (_appliedBuffBonus <= 0f)
            {
                return;
            }

            _statsProvider.Stats.AttackPower -= _appliedBuffBonus;
            _appliedBuffBonus = 0f;
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
