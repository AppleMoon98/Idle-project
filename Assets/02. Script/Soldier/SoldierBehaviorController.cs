using Behavior;
using Character;
using Combat;
using Core;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 이 병사 유닛(InstanceId)에 배정된 BehaviorProfileSO의 규칙을 주기적으로 평가해
    /// 상위 행동 모드를 결정하고, EnemyTracker/CharacterMover를 조합해 실제 움직임으로 옮긴다.
    /// EnemyTracker/Attacker/CharacterMover 자체의 로직은 전혀 건드리지 않고, 활성화 여부와
    /// 이동 목표만 조율한다(합성 원칙 — 기존 Combat 컴포넌트는 Soldier 도메인의 존재를 모른다).
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class SoldierBehaviorController : MonoBehaviour, ITickable
    {
        [SerializeField]
        private LayerMask enemyLayerMask;

        [SerializeField]
        private float decisionInterval = 0.5f;

        private Health _health;
        private CharacterStatsProvider _statsProvider;
        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private SoldierRosterService _roster;

        private int _instanceId;
        private Transform _retreatPoint;
        private float _elapsed;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _mover = GetComponent<CharacterMover>();
            _enemyTracker = GetComponent<EnemyTracker>();
            GameBootstrapper.Services?.TryGet(out _roster);
        }

        private void OnEnable()
        {
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

        /// <summary>
        /// 이 유닛이 어떤 로스터 유닛(instanceId)이고, 후퇴 시 어디로 갈지(retreatPoint)를 주입하고
        /// 즉시 한 번 평가한다. 스폰 직후 SoldierSpawner/SoldierRespawner가 호출한다.
        /// </summary>
        public void Initialize(int instanceId, Transform retreatPoint)
        {
            _instanceId = instanceId;
            _retreatPoint = retreatPoint;
            _elapsed = 0f;
            Evaluate();
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < decisionInterval)
            {
                return;
            }

            _elapsed = 0f;
            Evaluate();
        }

        private void Evaluate()
        {
            BehaviorMode mode = BehaviorMode.Engage;
            BehaviorProfileSO profile = null;

            if (_roster != null && _roster.TryGet(_instanceId, out OwnedSoldier owned))
            {
                profile = owned.BehaviorProfile;
            }

            if (profile != null && profile.Rules != null)
            {
                float maxHealth = _statsProvider.Stats.MaxHealth;
                float healthPercent = maxHealth > 0f ? _health.Current / maxHealth : 0f;
                var context = new BehaviorContext(healthPercent, transform.position, enemyLayerMask);

                foreach (BehaviorRuleEntry rule in profile.Rules)
                {
                    if (rule.Condition != null && rule.Condition.Evaluate(context))
                    {
                        mode = rule.Mode;
                        break;
                    }
                }
            }

            ApplyMode(mode);
        }

        private void ApplyMode(BehaviorMode mode)
        {
            switch (mode)
            {
                case BehaviorMode.Engage:
                    if (_enemyTracker != null)
                    {
                        _enemyTracker.enabled = true;
                    }
                    break;

                case BehaviorMode.Hold:
                    if (_enemyTracker != null)
                    {
                        _enemyTracker.enabled = false;
                    }
                    _mover.Target = null;
                    break;

                case BehaviorMode.Retreat:
                    if (_enemyTracker != null)
                    {
                        _enemyTracker.enabled = false;
                    }
                    _mover.Target = _retreatPoint;
                    _mover.StoppingDistance = 0f;
                    break;
            }
        }
    }
}
