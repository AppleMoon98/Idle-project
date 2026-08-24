using Character;
using Character.Events;
using Combat;
using Core;
using Managers;
using Stage.Events;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 탐지 범위(SkillSO.StrikeRange) 안 최근접 적 1체에게 독을 건다 - 즉시 데미지는 없고,
    /// SkillSO.TickInterval마다 (시전자 현재 공격력 + magnitude)만큼의 피해를 SkillSO.
    /// GetBuffDuration(레벨) 동안 반복한다. 각 틱은 Character.Health.TakeDamage(isPoison: true)로
    /// 적용되어 데미지 숫자가 평소와 다른 색(초록)으로 표시된다(Combat.DamageNumber 참고). 재시전
    /// 시 이전 대상의 독은 그냥 새 대상으로 교체된다(스탯을 되돌릴 필요가 없는 순수 피해 효과라
    /// SelfBuffSkillEffect처럼 "되돌리기"가 필요 없다). Stage.Events.CombatFieldResetEvent(스테이지
    /// 전환/던전 등 오버레이 진입·복귀)를 받으면 남은 시전을 무조건 무효화한다 - 안 그러면 독을
    /// 건 대상이 죽지 않고 그대로 다음 스테이지/던전까지 남아 엉뚱한 대상에게 계속 피해를 준다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class PoisonSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private PoolManager _pool;
        private CharacterStatsProvider _statsProvider;
        private Health _targetHealth;
        private float _damagePerTick;
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

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            GameBootstrapper.Events?.Subscribe<CombatFieldResetEvent>(OnCombatFieldReset);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            GameBootstrapper.Events?.Unsubscribe<CombatFieldResetEvent>(OnCombatFieldReset);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public bool HasTargetInRange(Transform origin, SkillSO definition)
        {
            return NearestHealthScan.FindNearest(origin.position, definition.StrikeRange, targetLayerMask) != null;
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            Health target = NearestHealthScan.FindNearest(origin.position, definition.StrikeRange, targetLayerMask);

            if (target == null)
            {
                return;
            }

            _targetHealth = target;
            _damagePerTick = _statsProvider.Stats.AttackPower + magnitude;
            _tickInterval = Mathf.Max(0.1f, definition.TickInterval);
            _tickElapsed = 0f;

            int level = GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService skillService)
                ? skillService.GetLevel(definition)
                : 0;
            _remaining = definition.GetBuffDuration(level);
            _isActive = true;

            SkillEffectVfx.SpawnAndPlay(_pool, definition, target.transform.position, vfxPoolCapacity, vfxPoolMaxSize, followTarget: target.transform);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            if (_targetHealth == null || _targetHealth.IsDead)
            {
                _isActive = false;
                return;
            }

            _remaining -= deltaTime;
            _tickElapsed += deltaTime;

            if (_tickElapsed >= _tickInterval)
            {
                _tickElapsed -= _tickInterval;
                _targetHealth.TakeDamage(_damagePerTick, isCritical: false, isPoison: true);
            }

            if (_remaining <= 0f)
            {
                _isActive = false;
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (_isActive && _targetHealth != null && evt.Character == _targetHealth.gameObject)
            {
                _isActive = false;
            }
        }

        private void OnCombatFieldReset(CombatFieldResetEvent evt)
        {
            _isActive = false;
            _targetHealth = null;
        }
    }
}
