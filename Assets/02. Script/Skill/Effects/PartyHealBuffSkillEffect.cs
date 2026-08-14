using Character;
using Core;
using Enhancement;
using Managers;
using Skill.Events;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 전투찬가 - 플레이어 자신과 병사 전체의 공격력을 (현재 공격력 × magnitude)만큼
    /// SkillSO.GetBuffDuration(레벨)초 동안 올리고, 그 지속시간 내내 매 프레임 최대체력의
    /// SkillSO.GetHealPercentPerSecond(레벨)만큼(초당 비율) 체력을 회복시킨다. 플레이어 본인의
    /// 공격력 버프/회복은 이 컴포넌트가 직접 적용하고, 병사 몫은 SkillSelfBuffAppliedEvent/
    /// SkillPartyHealAppliedEvent를 발행해 Soldier.SoldierStatReceiver가 각자 처리하게 한다 —
    /// SelfBuffSkillEffect와 같은 "본인은 직접 적용 + 이벤트로 병사에게도 알림" 구조를 그대로
    /// 따르되, 스탯 버프 하나에 회복 하나가 추가된 형태다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class PartyHealBuffSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private CharacterStatsProvider _statsProvider;
        private Health _health;
        private PoolManager _pool;
        private float _appliedAttackPowerBonus;
        private float _healPercentPerSecond;
        private float _remaining;

        private void Awake()
        {
            _statsProvider = GetComponentInParent<CharacterStatsProvider>();
            _health = GetComponentInParent<Health>();
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            RevertBonus();

            int level = GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService skillService)
                ? skillService.GetLevel(definition)
                : 0;
            float duration = definition.GetBuffDuration(level);
            float healPercentPerSecond = definition.GetHealPercentPerSecond(level);

            _appliedAttackPowerBonus = SkillBuffStatApplier.ApplyPercent(_statsProvider.Stats, EnhancementStatType.AttackPower, magnitude);
            _healPercentPerSecond = healPercentPerSecond;
            _remaining = duration;

            GameBootstrapper.Events?.Publish(new SkillSelfBuffAppliedEvent(EnhancementStatType.AttackPower, magnitude, duration));
            GameBootstrapper.Events?.Publish(new SkillPartyHealAppliedEvent(healPercentPerSecond, duration));

            Vector3 spawnPosition = origin.position + Vector3.up * definition.VfxHeightOffset;
            Transform followTarget = definition.VfxFollowCaster ? origin : null;
            SkillEffectVfx.SpawnAndPlay(_pool, definition, spawnPosition, vfxPoolCapacity, vfxPoolMaxSize, followTarget: followTarget);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _health.Heal(_statsProvider.Stats.MaxHealth * _healPercentPerSecond * deltaTime);

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                RevertBonus();
            }
        }

        /// <summary>
        /// 자기 자신과 병사 전체에게 거는 버프라 공격 대상이 필요 없다 - 항상 발동 가능하다.
        /// </summary>
        public bool HasTargetInRange(Transform origin, SkillSO definition)
        {
            return true;
        }

        private void RevertBonus()
        {
            if (_appliedAttackPowerBonus == 0f)
            {
                return;
            }

            SkillBuffStatApplier.Revert(_statsProvider.Stats, EnhancementStatType.AttackPower, _appliedAttackPowerBonus);
            _appliedAttackPowerBonus = 0f;
            _healPercentPerSecond = 0f;
        }
    }
}
