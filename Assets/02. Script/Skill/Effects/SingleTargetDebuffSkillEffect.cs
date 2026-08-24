using System.Collections.Generic;
using Character;
using Character.Events;
using Combat;
using Core;
using Enhancement;
using Stage.Events;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 탐지 범위(SkillSO.StrikeRange) 안 최근접 적 1체에게 지정된 스탯들을 낮추는 디버프를
    /// SkillSO.GetBuffDuration(레벨) 동안 거는 공통 골격. 어느 스탯을 낮출지는 파생 클래스가
    /// AffectedStats로 정한다(DebuffSkillEffect=쇠약=이속+공속, CurseSkillEffect=저주=최대체력+
    /// 공격력). magnitude는 "현재 값 대비 감소 비율"(최대 maxPercentReduction으로 클램프)로
    /// 해석되고 Skill.SkillBuffStatApplier로 적용/원복한다. 재시전 시 이전 대상의 디버프를 먼저
    /// 되돌린 뒤 새 대상에게 건다. 대상이 죽거나 전장이 초기화되면(스테이지 전환뿐 아니라 던전 등
    /// 오버레이 진입·복귀까지 포함, Stage.Events.CombatFieldResetEvent - 죽지 않고 풀로 강제
    /// 반환되는 경우까지 포함) 안전하게 정리한다 - StageMonsterScaler가 MaxHealth/AttackPower만
    /// 리스폰 시 재계산하고 MoveSpeed/AttackInterval은 그대로 두므로, 정리를 누락하면 디버프가
    /// 다음 재사용 개체에 그대로 눌러붙는다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public abstract class SingleTargetDebuffSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        [Range(0f, 0.95f)]
        private float maxPercentReduction = 0.9f;

        private CharacterStatsProvider _targetStatsProvider;
        private Health _targetHealth;
        private readonly Dictionary<EnhancementStatType, float> _appliedDeltas = new();
        private float _remaining;
        private bool _isActive;

        /// <summary>
        /// 이 디버프가 낮출 스탯 목록. 파생 클래스가 고정 배열로 제공한다.
        /// </summary>
        protected abstract IReadOnlyList<EnhancementStatType> AffectedStats { get; }

        private void OnEnable()
        {
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

            if (target == null || !target.TryGetComponent(out CharacterStatsProvider statsProvider))
            {
                return;
            }

            RevertIfActive();

            float ratio = -Mathf.Min(magnitude, maxPercentReduction);
            RuntimeStats stats = statsProvider.Stats;

            foreach (EnhancementStatType statType in AffectedStats)
            {
                _appliedDeltas[statType] = SkillBuffStatApplier.ApplyPercent(stats, statType, ratio);
            }

            _targetStatsProvider = statsProvider;
            _targetHealth = target;

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

            if (_targetHealth == null || _targetHealth.IsDead)
            {
                _isActive = false;
                return;
            }

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                Revert();
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            // 대상이 죽으면 그냥 추적을 멈춘다 - 죽는 순간 이후의 스탯은 의미가 없고, 다음 스폰 때
            // StageMonsterScaler 등이 기준치부터 다시 계산하므로 되돌리든 안 되돌리든 결과가 같다.
            if (_isActive && _targetHealth != null && evt.Character == _targetHealth.gameObject)
            {
                _isActive = false;
                _appliedDeltas.Clear();
                _targetStatsProvider = null;
                _targetHealth = null;
            }
        }

        private void OnCombatFieldReset(CombatFieldResetEvent evt)
        {
            // 스테이지 전환/던전 등 오버레이 진입·복귀 시 남아있는 적은 죽지 않고 그대로 풀에
            // 강제 반환될 수 있다(Stage.StageProgressTracker.ReleaseRemaining) - 이때는
            // CharacterDiedEvent가 뜨지 않으므로 여기서 명시적으로 되돌린다.
            RevertIfActive();
        }

        private void RevertIfActive()
        {
            if (_isActive)
            {
                Revert();
            }
        }

        private void Revert()
        {
            _isActive = false;

            if (_targetStatsProvider != null)
            {
                RuntimeStats stats = _targetStatsProvider.Stats;

                foreach (KeyValuePair<EnhancementStatType, float> pair in _appliedDeltas)
                {
                    SkillBuffStatApplier.Revert(stats, pair.Key, pair.Value);
                }
            }

            _appliedDeltas.Clear();
            _targetStatsProvider = null;
            _targetHealth = null;
        }
    }
}
