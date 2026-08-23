using Behavior;
using Character;
using Combat;
using Core;
using Services;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 이 병사 유닛(InstanceId)에 배정된 BehaviorProfileSO의 규칙을 주기적으로 평가해
    /// 상위 행동 모드를 결정하고, EnemyTracker/CharacterMover를 조합해 실제 움직임으로 옮긴다.
    /// EnemyTracker/Attacker/CharacterMover 자체의 로직은 전혀 건드리지 않고, 활성화 여부와
    /// 이동 목표만 조율한다(합성 원칙 — 기존 Combat 컴포넌트는 Soldier 도메인의 존재를 모른다).
    /// 프로필 평가보다 먼저, 자기 자신이 최광각 고정 범위(EnemyTracker/Attacker와 동일 기준,
    /// 줌 배율과 무관) 밖에 있는지를 최우선으로 확인한다 — 그 범위 밖에서 교전 중이면 몬스터가
    /// 영영 도달하지 못하는 상황이 생기므로, 그 경우 다른 어떤 로직보다 앞서 범위 안으로
    /// 복귀시킨다. 배치 슬롯 스폰 지점(_retreatPoint)은 재사용하지 않는다 — 스폰 지점 자체가
    /// 이 범위 밖(대기 구역)에 있을 수 있어, 그리로 보내면 여전히 범위 밖이라 이 우선순위가
    /// 영원히 해소되지 않는다. 대신 현재 위치를 그 범위 안쪽으로 클램프한 지점(반드시 범위 안)을
    /// 계산해 그리로 이동시킨다.
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
        private float kiteStepDistance = 2f;

        [SerializeField]
        private float kiteScreenMargin = 0.1f;

        [SerializeField]
        private float screenReturnMargin = 0.15f;

        private Health _health;
        private CharacterStatsProvider _statsProvider;
        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private RangedAttackBehavior _rangedAttack;
        private BearCharge _bearCharge;
        private FormationFollower _formationFollower;
        private RangedKiter _formationKiter;
        private SoldierRosterService _roster;
        private CameraFollowService _cameraFollowService;
        private SquadMovementSyncService _squadMovementSync;
        private Transform _kiteAnchor;
        private Transform _returnAnchor;

        private int _instanceId;
        private Transform _retreatPoint;
        private float _elapsed;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _mover = GetComponent<CharacterMover>();
            _enemyTracker = GetComponent<EnemyTracker>();
            _rangedAttack = GetComponent<RangedAttackBehavior>();
            _bearCharge = GetComponent<BearCharge>();
            _formationFollower = GetComponent<FormationFollower>();
            _formationKiter = GetComponent<RangedKiter>();
            GameBootstrapper.Services?.TryGet(out _roster);
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
            GameBootstrapper.Services?.TryGet(out _squadMovementSync);

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
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// Awake에서 만든 독립 앵커 GameObject는 풀링 대상이 아니라 이 컴포넌트가 직접 소유하므로,
        /// 파괴 시 함께 정리하지 않으면 풀 eviction(maxSize 초과 시 Object.Destroy 경로) 때마다
        /// 고아 GameObject가 남는다.
        /// </summary>
        private void OnDestroy()
        {
            if (_returnAnchor != null)
            {
                Destroy(_returnAnchor.gameObject);
            }

            if (_kiteAnchor != null)
            {
                Destroy(_kiteAnchor.gameObject);
            }
        }

        /// <summary>
        /// 이 유닛이 어떤 로스터 유닛(instanceId)이고, 후퇴 시 어디로 갈지(retreatPoint)를 주입하고
        /// 즉시 한 번 평가한다. 스폰 직후 SoldierSpawner/SoldierRespawner가 호출한다.
        /// BearCharge/FormationFollower는 "탐지 범위 안에 아무 대상(몬스터)도 없을 때의
        /// 기본 위협"으로 플레이어를 주입받도록 설계돼 있지만(몬스터 쪽 MonsterSpawner.SpawnOne이
        /// IMonsterMovementInitializer.Initialize(playerTransform)를 호출하는 것과 같은 코드 형태),
        /// 그건 몬스터 입장(적이 없으면 플레이어를 향해 간다)에서만 의미가 있다. 병사에게 그대로
        /// 적용하면 스테이지 시작 직후처럼 근처에 몬스터가 하나도 없을 때 아군인 플레이어를
        /// "위협"으로 삼아 쫓아가거나 주위를 도는 버그가 된다(실사용 중 발견) — 그래서 병사 쪽은
        /// 항상 null을 넘겨, 각 컴포넌트에 이미 있는 "위협 없음 → 제자리 대기" 폴백을 그대로
        /// 타도록 한다. 이 컴포넌트들이 없는 병과(보병/궁병 등)에서는 자연히 아무 일도 하지 않는다.
        /// </summary>
        public void Initialize(int instanceId, Transform retreatPoint)
        {
            // 풀에서 재사용되는 인스턴스가 과거(예: 넉백 도중 사망)에 disabled 상태로 반환됐을 수
            // 있다 - Unity는 GameObject 재활성화만으로 이미 enabled=false인 컴포넌트를 다시 켜주지
            // 않으므로, 새로 스폰될 때마다 명시적으로 되살린다(Character.KnockbackReceiver.OnDisable
            // 이 이 문제의 근본 원인을 막지만, 여기서도 한 번 더 보장해 향후 다른 원인으로 disabled
            // 된 인스턴스가 재사용되더라도 안전하게 시작하도록 한다).
            enabled = true;

            if (_enemyTracker != null)
            {
                _enemyTracker.enabled = true;
            }

            _instanceId = instanceId;
            _retreatPoint = retreatPoint;
            _elapsed = 0f;

            if (_bearCharge != null)
            {
                _bearCharge.Initialize(null);
            }

            if (_formationFollower != null)
            {
                _formationFollower.Initialize(null);
            }

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
            // 실시간 카메라 화면(줌 배율에 따라 좁아짐)이 아니라 EnemyTracker/Attacker와 동일한
            // 최광각 고정 범위 기준으로 판정한다 - 그래야 병사가 그 범위 안에 있는데도(=적을
            // 인지/공격할 수 있는 실제 영역 안인데도) 줌인으로 좁아진 화면 밖이라는 이유만으로
            // 교전을 계속 건너뛰고 화면 복귀만 반복하는 일이 없다.
            if (_cameraFollowService != null
                && !CameraVisibility.IsWithinBounds(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), transform.position, screenReturnMargin))
            {
                // 화면 복귀 중에도 부대 최저속 동기화 대상이다 — 애초에 이 기능은 여러 병사가
                // 화면 밖에서 각자 다른 속도로 복귀하며 뭉쳐 보이던 문제를 해소하려는 것이라,
                // 이 경로에서 클램프를 풀어버리면 정작 그 문제가 그대로 남는다.
                _squadMovementSync?.SetMarching(gameObject, true);
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

            // Engage 모드이고, 가장 가까운 적도 자기 공격 사거리 밖일 때만 "행군 중"으로 취급해
            // 부대 최저속 클램프 대상이 된다 — Hold/Retreat이거나 적이 실제로 사거리 안까지 왔으면
            // (각 이동 드라이버가 뒤이어 그 적을 쫓거나 공격하기 시작하므로) 클램프를 풀어 자기 본연
            // 속도로 돌아간다. FindNearestTargetableEnemy() 자체는 화면 전체(최광각 고정 범위)를
            // 스캔하므로, 예전에는 "적이 화면 어딘가에 존재하기만 하면" 여기서 곧바로 행군을
            // 그만두는 버그가 있었다 — 사거리와 무관하게 스폰 직후 거의 즉시 대형이 풀려, 부대
            // 동기화 이동 속도(Soldier.SquadMovementSyncService)가 적용될 기회를 못 얻는 원인이었다.
            Health nearestEnemy = FindNearestTargetableEnemy();
            bool enemyInRange = nearestEnemy != null
                && Vector3.Distance(transform.position, nearestEnemy.transform.position) <= _statsProvider.Stats.AttackRange;
            bool isMarching = mode == BehaviorMode.Engage && !enemyInRange;
            _squadMovementSync?.SetMarching(gameObject, isMarching);

            // ApplyMode에는 이 유닛 자신의 로컬 판정이 아니라 부대 전체의 집계값을 넘긴다 —
            // 부대원 중 하나라도 교전에 들어가면(GetEffectiveMarching이 false) 아직 적을 못 찾은
            // 나머지도 즉시 함께 전투태세로 전환된다(궁병의 대형 이탈 등). 기마병은
            // GetEffectiveMarching이 항상 자기 로컬 값을 그대로 돌려주므로 원래처럼 계속 각개행동.
            bool effectiveMarching = _squadMovementSync != null ? _squadMovementSync.GetEffectiveMarching(gameObject) : isMarching;

            ApplyMode(mode, effectiveMarching);
        }

        /// <summary>
        /// 최광각 고정 범위 밖에 있을 때의 최우선 행동 — 교전 판정을 전부 건너뛰고, 현재 위치를
        /// 그 범위 안쪽(마진 포함)으로 클램프한 지점으로 즉시 복귀시킨다. 이 지점은 정의상 항상
        /// 범위 안이므로, 도착하면 다음 평가 주기에서 이 우선순위가 자연히 해소된다.
        /// </summary>
        private void ReturnToScreen()
        {
            if (_enemyTracker != null)
            {
                _enemyTracker.enabled = false;
            }

            _returnAnchor.position = CameraVisibility.ClampToBounds(
                _cameraFollowService.HomeLocalPosition,
                _cameraFollowService.GetWorldBoundsHalfExtent(),
                transform.position,
                screenReturnMargin);
            _mover.Target = _returnAnchor;
            _mover.StoppingDistance = 0f;
        }

        private void ApplyMode(BehaviorMode mode, bool isMarching)
        {
            switch (mode)
            {
                case BehaviorMode.Engage:
                    if (_bearCharge != null)
                    {
                        if (_enemyTracker != null)
                        {
                            _enemyTracker.enabled = false;
                        }

                        _bearCharge.enabled = true;
                    }
                    else if (_formationFollower != null && _rangedAttack != null)
                    {
                        // 궁병(FormationFollower + RangedAttackBehavior를 함께 가짐, 방패벽 부대의
                        // 2열 이하로 배치된 경우)은 창병과 달리 Combat.FormationFollower 자체의 전투
                        // 핸드오프(HandOffToKiter → Combat.RangedKiter)를 타지 않는다 — 궁병의
                        // "원래 방식"은 이미 이 컨트롤러 안의 TickRangedKiting()이므로, 마칭 중이고
                        // 리더가 배정돼 있을 때만 대형을 추종하고, 전투가 시작되는 순간(isMarching이
                        // false로 바뀜, 리더 유무와 무관) 곧바로 원래 카이팅으로 돌아간다.
                        if (_enemyTracker != null)
                        {
                            _enemyTracker.enabled = false;
                        }

                        if (isMarching && _formationFollower.HasLeader)
                        {
                            _formationFollower.enabled = true;
                        }
                        else
                        {
                            _formationFollower.enabled = false;
                            TickRangedKiting();
                        }
                    }
                    else if (_formationFollower != null)
                    {
                        if (_enemyTracker != null)
                        {
                            _enemyTracker.enabled = false;
                        }

                        // FormationFollower가 이미 RangedKiter로 넘긴 상태(Combat.FormationFollower의
                        // 되돌리지 않는 일회성 전환, section EY)라면 다시 켜서 되돌리지 않는다 —
                        // 그 상태에서 그대로 두는 것이 곧 "카이팅 유지"다.
                        bool alreadyHandedOff = _formationKiter != null && _formationKiter.enabled;

                        if (!alreadyHandedOff)
                        {
                            _formationFollower.enabled = true;
                        }
                    }
                    else if (_rangedAttack != null)
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
                    DisableAllMovementDrivers();
                    _mover.Target = null;
                    break;

                case BehaviorMode.Retreat:
                    DisableAllMovementDrivers();
                    _mover.Target = _retreatPoint;
                    _mover.StoppingDistance = 0f;
                    break;
            }
        }

        /// <summary>
        /// Hold/Retreat 진입 시 어떤 병과든 이동을 완전히 멈추도록, 이 컨트롤러가 조율하는 이동
        /// 드라이버를 전부 끈다. FormationFollower가 이미 RangedKiter로 넘긴 상태였더라도 여기서는
        /// 둘 다 끈다 — 다음에 다시 Engage로 돌아오면 대형 추종부터 새로 시작하는 게 자연스럽다
        /// (카이팅 중이던 자리를 그대로 재개하지 않음).
        /// </summary>
        private void DisableAllMovementDrivers()
        {
            if (_enemyTracker != null)
            {
                _enemyTracker.enabled = false;
            }

            if (_bearCharge != null)
            {
                _bearCharge.enabled = false;
            }

            if (_formationFollower != null)
            {
                _formationFollower.enabled = false;
            }

            if (_formationKiter != null)
            {
                _formationKiter.enabled = false;
            }
        }

        /// <summary>
        /// 원거리 병사 전용 이동 로직 — EnemyTracker를 대신해 이 컨트롤러가 직접 타겟을 찾고,
        /// 사거리 밖이면 접근, 사거리 안이면 대기, 사거리 안으로 적이 너무 가까워지면 반대
        /// 방향으로 후퇴한다(카이팅). 공격 직후에도 멈추지 않고 계속 움직인다(공격 후 경직
        /// 패널티 없음 — 실사용 피드백으로 제거됨). 매 decisionInterval마다 재평가된다.
        /// </summary>
        private void TickRangedKiting()
        {
            Health nearestEnemy = FindNearestTargetableEnemy();

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

        /// <summary>
        /// 별도의 거리 기반 탐지 범위 없이, 최광각 고정 범위(EnemyTracker와 동일 기준) 안의
        /// 후보만으로 최근접 적을 찾는다. CameraFollowService를 못 구했으면 판정 기준 범위
        /// 자체가 없으므로 null.
        /// </summary>
        private Health FindNearestTargetableEnemy()
        {
            if (_cameraFollowService == null)
            {
                return null;
            }

            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            NearestHealthScan.ForEachAliveCandidateInBounds(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), enemyLayerMask, (candidate, health) =>
            {
                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            });

            return nearest;
        }

        /// <summary>
        /// Combat.KiteRetreatCalculator에 위임 — 최광각 고정 범위 기준(줌 배율과 무관, 이동속도가
        /// 스탯인 이상 후퇴 가능 범위가 줌으로 늘거나 줄면 안 된다)으로 적 반대 방향 후보 각도 중
        /// 적과 가장 멀어지는 지점을 고른다. CameraFollowService를 못 구했으면 후퇴 지점을 계산할
        /// 수 없으므로 false(호출부가 제자리 사수로 처리).
        /// </summary>
        private bool TryFindKiteRetreatPoint(Vector3 selfPosition, Vector3 enemyPosition, out Vector3 retreatPoint)
        {
            if (_cameraFollowService == null)
            {
                retreatPoint = Vector3.zero;
                return false;
            }

            return KiteRetreatCalculator.TryFindRetreatPoint(
                _cameraFollowService.HomeLocalPosition,
                _cameraFollowService.GetWorldBoundsHalfExtent(),
                selfPosition,
                enemyPosition,
                kiteStepDistance,
                kiteScreenMargin,
                out retreatPoint);
        }
    }
}
