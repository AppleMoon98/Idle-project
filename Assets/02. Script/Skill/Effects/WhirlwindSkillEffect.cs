using Character;
using Combat;
using Core;
using Managers;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 시전자 주변 SkillSO.AreaRadius 안의 적 전체에게, SkillSO.TickInterval마다 (시전자 현재
    /// 공격력 + magnitude)만큼의 피해를 SkillSO.GetBuffDuration(레벨) 동안 반복해서 준다. 매 틱마다
    /// 시전자 위치에 이펙트를 새로 재생해(VfxFollowCaster 여부와 무관하게 항상 시전자를 따라감)
    /// "캐릭터 주변에 계속 이펙트가 나오는" 것처럼 보이게 한다 - 하나의 이펙트가 계속 재생되는
    /// 대신 SkillEffectVfx의 기존 "1회 재생 후 자동 반납" 파이프라인을 틱마다 재사용하는 방식이라
    /// 새 루프 이펙트 컴포넌트가 필요 없다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class WhirlwindSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private PoolManager _pool;
        private CharacterStatsProvider _statsProvider;
        private Transform _origin;
        private SkillSO _definition;
        private float _magnitude;
        private float _tickInterval;
        private float _tickElapsed;
        private float _remaining;
        private bool _isActive;

        private void Awake()
        {
            _statsProvider = GetComponentInParent<CharacterStatsProvider>();
        }

        private void OnEnable()
        {
            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public bool HasTargetInRange(Transform origin, SkillSO definition)
        {
            return NearestHealthScan.FindNearest(origin.position, definition.AreaRadius, targetLayerMask) != null;
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            _origin = origin;
            _definition = definition;
            _magnitude = magnitude;
            _tickInterval = Mathf.Max(0.1f, definition.TickInterval);
            _tickElapsed = _tickInterval; // 첫 틱이 다음 프레임을 기다리지 않고 즉시 나가도록.

            int level = GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService skillService)
                ? skillService.GetLevel(definition)
                : 0;
            _remaining = definition.GetBuffDuration(level);
            _isActive = true;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _remaining -= deltaTime;
            _tickElapsed += deltaTime;

            if (_tickElapsed >= _tickInterval)
            {
                _tickElapsed -= _tickInterval;
                Pulse();
            }

            if (_remaining <= 0f)
            {
                _isActive = false;
            }
        }

        private void Pulse()
        {
            float damage = _statsProvider.Stats.AttackPower + _magnitude;

            NearestHealthScan.ForEachAliveCandidate(_origin.position, _definition.AreaRadius, targetLayerMask, (candidate, health) =>
            {
                health.TakeDamage(damage);
            });

            SkillEffectVfx.SpawnAndPlay(_pool, _definition, _origin.position, vfxPoolCapacity, vfxPoolMaxSize, followTarget: _origin);
        }
    }
}
