using Combat;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 방패 보병 몸 스프라이트 시트 애니메이션(Idle/Run/Guard/Attack1/Attack2)을 기존 신호만으로
    /// 재생한다 - SpearmanAnimationController와 같은 방향. CharacterMover.Target/StoppingDistance만
    /// 으로 IsMoving/IsInRange를 계산한다("Guard"는 Spearman의 "Defence"와 동일 역할 - 사거리 안에
    /// 들어오면 사용). IsAttacking은 같은 오브젝트의 Attacker가 발행하는
    /// AttackWindupStarted/AttackPerformed로 구동한다.
    ///
    /// 공격 애니메이션은 Attack1 -> Attack2 두 클립이 이어져 재생된다. Attack1 진입은 AnyState가
    /// 아니라 Idle/Run/Guard 각각에서 Attack1로 가는 개별 전이(조건 IsAttacking==true)로 만들었다 -
    /// 처음엔 AnyState->Attack1 하나로 만들었는데, AnyState 전이는 조건이 계속 참인 한 매 프레임
    /// 다시 발동한다(현재 상태가 이미 그 목적지와 같아도 예외 없음)는 걸 실사용 중 발견했다.
    /// IsAttacking이 Attack1+Attack2 재생 내내 계속 true라서, Attack1이 재생되는 동안 AnyState가
    /// 매 프레임 자기 자신을 다시 트리거해 클립이 몇 번이고 재시작됐고, 그 재시작에 실제 시간을
    /// 다 써버린 탓에 (Attacker의 공격 주기는 재생 상태와 무관하게 흘러가므로) Attack2가 시작될
    /// 때는 이미 실제 타격(AttackPerformed)이 코앞이라 Attack2가 몇 프레임만 스치듯 보이고 바로
    /// Guard로 끊겼다("두 번째 스윙이 안 보인다"는 제보의 실제 원인). Idle/Run/Guard발 개별 전이는
    /// 그 소스 상태에 있을 때만 평가되므로, Attack1에 들어간 순간부터는 아예 재검사 대상이 아니라
    /// 이 문제 자체가 구조적으로 발생할 수 없다. Attack1->Attack2는 원래대로 클립이 끝까지 재생된
    /// 뒤 자동으로 이어지는 전이(hasExitTime, 조건 없음)를 그대로 쓴다.
    ///
    /// 공격 동안(IsAttacking) CharacterMover를 잠깐 꺼서 제자리에 멈춘다 - SpearmanAnimationController
    /// 가 겪은 것과 같은 이유(Attacker의 독립적인 재탐색 타겟과 CharacterMover.Target이 다를 수 있어
    /// "공격 자세인데 미끄러지듯 이동" 하는 문제 방지). OnDisable에서도 무조건 다시 켠다.
    ///
    /// AttackPerformed가 항상 뒤따라온다는 보장은 없다(Combat.Attacker.Tick() 참고) - maxAttackHoldSeconds
    /// 타임아웃으로 방어한다.
    ///
    /// 데미지는 사이클당 2회 들어간다 - Attack2가 끝나는 시점(Combat.Attacker의 정상적인
    /// AttackPerformed, Combat.MeleeAttackBehavior를 거쳐 이미 처리됨)과, Attack1이 끝나는
    /// 시점(attack1Duration 경과, 이 컨트롤러가 직접 처리) 둘 다 한 번씩. 후자는 Combat.Attacker의
    /// 정규 공격 주기와 무관한 별도 타격이라 War.Boss.WarBossPatternRunner/Combat.CavalryCharge의
    /// 돌진 명중 판정과 같은 방식(Health.TakeDamage 직접 호출, IAttackBehavior 사이클 밖)으로
    /// 처리한다. AttackPower는 사이클당 2회 타격을 반영해 기존의 절반으로 낮춰뒀다(DPS 유지).
    ///
    /// 스프라이트 시트가 기본적으로 오른쪽을 보고 그려져 있어서, Target이 왼쪽에 있으면
    /// SpriteRenderer.flipX로 좌우 반전한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class ShieldBearerAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsInRangeHash = Animator.StringToHash("IsInRange");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        [SerializeField]
        private float maxAttackHoldSeconds = 1.2f;

        /// <summary>
        /// Attack1 클립 실제 길이(초) - 이 시간이 지나면 Attack1 종료 타격을 직접 적용한다.
        /// Warrior_Attack1.anim(4프레임, 10fps)의 길이와 일치해야 한다.
        /// </summary>
        [SerializeField]
        private float attack1Duration = 0.4f;

        [SerializeField]
        private LayerMask enemyLayerMask;

        private Animator _animator;
        private CharacterMover _mover;
        private Attacker _attacker;
        private CharacterStatsProvider _statsProvider;
        private SpriteRenderer _spriteRenderer;

        private bool _isAttacking;
        private bool _firstHitDealt;
        private float _attackElapsed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _mover = GetComponent<CharacterMover>();
            _attacker = GetComponent<Attacker>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);

            if (_attacker != null)
            {
                _attacker.AttackWindupStarted += OnWindupStarted;
                _attacker.AttackPerformed += OnAttackPerformed;
            }
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);

            if (_attacker != null)
            {
                _attacker.AttackWindupStarted -= OnWindupStarted;
                _attacker.AttackPerformed -= OnAttackPerformed;
            }

            EndAttack();
        }

        private void OnWindupStarted(Health target)
        {
            _animator.SetBool(IsAttackingHash, true);
            _mover.enabled = false;
            _isAttacking = true;
            _firstHitDealt = false;
            _attackElapsed = 0f;
        }

        private void OnAttackPerformed()
        {
            EndAttack();
        }

        private void EndAttack()
        {
            _animator.SetBool(IsAttackingHash, false);
            _mover.enabled = true;
            _isAttacking = false;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isAttacking)
            {
                _attackElapsed += deltaTime;

                if (!_firstHitDealt && _attackElapsed >= attack1Duration)
                {
                    DealFirstHit();
                    _firstHitDealt = true;
                }

                if (_attackElapsed >= maxAttackHoldSeconds)
                {
                    EndAttack();
                }
            }

            bool hasTarget = _mover.Target != null;
            float distance = hasTarget ? Vector3.Distance(transform.position, _mover.Target.position) : 0f;

            // StoppingDistance는 실제 전투 사거리(EnemyTracker 등이 AttackRange로 설정)뿐 아니라
            // 순수 이동 목적(화면 복귀/후퇴/집결 등, 전부 0으로 설정 - Combat.RangedKiter 등 전체
            // 코드베이스에서 예외 없이 지켜지는 관례)으로도 재사용된다. 0이면 "지금 목표는 적이
            // 아니라 그냥 지나가는 지점"이라는 뜻이므로 Guard 판정에서 제외해야 한다 - 그렇지
            // 않으면 스테이지 시작 직후 화면 밖에서 복귀하는 도중(ScreenReturnAnchor, StoppingDistance
            // 0) 그 지점에 도착할 때마다 distance<=0이 참이 되어 근처에 적이 전혀 없는데도 Guard
            // 자세를 취하는 것처럼 보인다(실사용 중 발견).
            bool isInRange = hasTarget && _mover.StoppingDistance > 0f && distance <= _mover.StoppingDistance;
            bool isMoving = hasTarget && !isInRange;

            _animator.SetBool(IsMovingHash, isMoving);
            _animator.SetBool(IsInRangeHash, isInRange);
            UpdateFacing();
        }

        /// <summary>
        /// Attack1이 끝나는 순간의 타격 - Combat.Attacker의 정규 공격 주기와 별개로, 이 컨트롤러가
        /// 직접 타겟을 찾아 데미지를 적용한다(War.Boss.WarBossPatternRunner/Combat.CavalryCharge의
        /// 직접 Health.TakeDamage 호출과 같은 방식). 사거리 안에 살아있는 적이 없으면 조용히 무시.
        /// </summary>
        private void DealFirstHit()
        {
            RuntimeStats stats = _statsProvider.Stats;
            Health target = NearestHealthScan.FindNearest(transform.position, stats.AttackRange, enemyLayerMask);

            if (target == null)
            {
                return;
            }

            bool isCritical = Random.value < stats.CriticalChance;
            float damage = isCritical ? stats.AttackPower * (1f + stats.CriticalDamageMultiplier) : stats.AttackPower;

            target.TakeDamage(damage, isCritical);
        }

        private void UpdateFacing()
        {
            if (_mover.Target == null)
            {
                return;
            }

            float dx = _mover.Target.position.x - transform.position.x;

            if (Mathf.Abs(dx) > 0.01f)
            {
                _spriteRenderer.flipX = dx < 0f;
            }
        }
    }
}
