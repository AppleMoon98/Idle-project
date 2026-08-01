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
    /// 프로필 평가보다 먼저, 자기 자신이 화면 밖에 있는지를 최우선으로 확인한다 — 화면 밖에서
    /// 교전 중이면 몬스터가 영영 도달하지 못하는 상황이 생기므로, 그 경우 다른 어떤 로직보다
    /// 앞서 화면 안으로 복귀시킨다. 배치 슬롯 스폰 지점(_retreatPoint)은 재사용하지 않는다 —
    /// 스폰 지점 자체가 화면 밖(대기 구역)에 있을 수 있어, 그리로 보내면 여전히 화면 밖이라
    /// 이 우선순위가 영원히 해소되지 않는다. 대신 현재 위치를 화면 뷰포트 안쪽으로 클램프한
    /// 지점(반드시 화면 안)을 계산해 그리로 이동시킨다.
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

        [SerializeField]
        private float postAttackLockDuration = 0.5f;

        [SerializeField]
        private float kiteStepDistance = 2f;

        [SerializeField]
        private float kiteScreenMargin = 0.1f;

        [SerializeField]
        private float screenReturnMargin = 0.15f;

        private static readonly float[] KiteCandidateAngles = { 0f, 45f, -45f, 90f, -90f };

        private Health _health;
        private CharacterStatsProvider _statsProvider;
        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private Attacker _attacker;
        private RangedAttackBehavior _rangedAttack;
        private SoldierRosterService _roster;
        private Camera _camera;
        private Transform _kiteAnchor;
        private Transform _returnAnchor;

        private int _instanceId;
        private Transform _retreatPoint;
        private float _elapsed;
        private float _postAttackLockRemaining;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _mover = GetComponent<CharacterMover>();
            _enemyTracker = GetComponent<EnemyTracker>();
            _attacker = GetComponent<Attacker>();
            _rangedAttack = GetComponent<RangedAttackBehavior>();
            _camera = Camera.main;
            GameBootstrapper.Services?.TryGet(out _roster);

            // 이동 목표로 쓸 앵커는 병사의 자식으로 붙이면 안 된다 — 자식이면 부모(병사)가 움직일
            // 때마다 같은 상대 오프셋을 유지하며 같이 이동해버려서, CharacterMover가 "항상 같은
            // 거리만큼 앞서 도망가는 목표"를 영원히 쫓는 꼴이 된다(고정된 세계 좌표가 아니게 됨).
            // 반드시 독립된(부모 없는) Transform이어야 실제로 고정된 지점 역할을 한다.
            _returnAnchor = new GameObject("ScreenReturnAnchor").transform;

            if (_rangedAttack != null)
            {
                _kiteAnchor = new GameObject("KiteAnchor").transform;
            }
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (_attacker != null)
            {
                _attacker.AttackPerformed += OnAttackPerformed;
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }

            if (_attacker != null)
            {
                _attacker.AttackPerformed -= OnAttackPerformed;
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
            _postAttackLockRemaining = 0f;
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

        private void OnAttackPerformed()
        {
            _postAttackLockRemaining = postAttackLockDuration;
        }

        private void Evaluate()
        {
            if (!CameraVisibility.IsOnScreen(_camera, transform.position))
            {
                ReturnToScreen();
                return;
            }

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

        /// <summary>
        /// 화면 밖에 있을 때의 최우선 행동 — 교전 판정을 전부 건너뛰고, 현재 위치를 화면 뷰포트
        /// 안쪽(마진 포함)으로 클램프한 지점으로 즉시 복귀시킨다. 이 지점은 정의상 항상 화면 안이므로,
        /// 도착하면 다음 평가 주기에서 이 우선순위가 자연히 해소된다.
        /// </summary>
        private void ReturnToScreen()
        {
            if (_enemyTracker != null)
            {
                _enemyTracker.enabled = false;
            }

            Vector3 viewportPoint = _camera.WorldToViewportPoint(transform.position);
            float clampedX = Mathf.Clamp(viewportPoint.x, screenReturnMargin, 1f - screenReturnMargin);
            float clampedY = Mathf.Clamp(viewportPoint.y, screenReturnMargin, 1f - screenReturnMargin);

            _returnAnchor.position = _camera.ViewportToWorldPoint(new Vector3(clampedX, clampedY, viewportPoint.z));
            _mover.Target = _returnAnchor;
            _mover.StoppingDistance = 0f;
        }

        private void ApplyMode(BehaviorMode mode)
        {
            switch (mode)
            {
                case BehaviorMode.Engage:
                    if (_rangedAttack != null)
                    {
                        if (_enemyTracker != null)
                        {
                            _enemyTracker.enabled = false;
                        }

                        TickRangedKiting();
                    }
                    else if (_enemyTracker != null)
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

        /// <summary>
        /// 원거리 병사 전용 이동 로직 — EnemyTracker를 대신해 이 컨트롤러가 직접 타겟을 찾고,
        /// 사거리 밖이면 접근, 사거리 안이면 대기, 공격 직후에는 잠깐 경직 후 최근접 적 반대
        /// 방향으로 후퇴한다(카이팅). 매 decisionInterval마다 재평가된다.
        /// </summary>
        private void TickRangedKiting()
        {
            if (_postAttackLockRemaining > 0f)
            {
                _postAttackLockRemaining -= decisionInterval;
                _mover.Target = null;
                return;
            }

            float detectionRange = _enemyTracker != null ? _enemyTracker.DetectionRange : 20f;
            Health nearestEnemy = FindNearestOnScreenEnemy(detectionRange);

            if (nearestEnemy == null)
            {
                _mover.Target = null;
                return;
            }

            float attackRange = _statsProvider.Stats.AttackRange;
            float distance = Vector3.Distance(transform.position, nearestEnemy.transform.position);

            if (distance > attackRange)
            {
                _mover.Target = nearestEnemy.transform;
                _mover.StoppingDistance = attackRange;
                return;
            }

            if (TryFindKiteRetreatPoint(transform.position, nearestEnemy.transform.position, out Vector3 retreatPoint))
            {
                _kiteAnchor.position = retreatPoint;
                _mover.Target = _kiteAnchor;
                _mover.StoppingDistance = 0f;
            }
            else
            {
                // 화면 안 어느 방향으로도 물러날 여유가 없다 — 화면 밖으로 밀려나지 않도록 그 자리에서 버틴다.
                _mover.Target = null;
            }
        }

        private Health FindNearestOnScreenEnemy(float range)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, range, enemyLayerMask);

            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                if (!CameraVisibility.IsOnScreen(_camera, candidate.transform.position))
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 적 반대 방향을 기준으로 몇 가지 후보 각도(0/±45/±90도)를 시도해, 화면 안(마진 포함)에
        /// 남는 후보 중 적과 가장 멀어지는 지점을 고른다. 유효한 후보가 하나도 없으면 false.
        /// </summary>
        private bool TryFindKiteRetreatPoint(Vector3 selfPosition, Vector3 enemyPosition, out Vector3 retreatPoint)
        {
            Vector3 awayDirection = (selfPosition - enemyPosition).normalized;

            Vector3 bestPoint = Vector3.zero;
            float bestSqrDistance = -1f;
            bool found = false;

            foreach (float angle in KiteCandidateAngles)
            {
                Vector3 candidateDirection = Quaternion.Euler(0f, 0f, angle) * awayDirection;
                Vector3 candidatePoint = selfPosition + candidateDirection * kiteStepDistance;

                if (!CameraVisibility.IsOnScreen(_camera, candidatePoint, kiteScreenMargin))
                {
                    continue;
                }

                float sqrDistance = (candidatePoint - enemyPosition).sqrMagnitude;

                if (sqrDistance > bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = candidatePoint;
                    found = true;
                }
            }

            retreatPoint = bestPoint;
            return found;
        }
    }
}
