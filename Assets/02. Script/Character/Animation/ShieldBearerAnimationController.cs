using Character;
using Combat;
using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 방패 보병 몸 스프라이트 시트 애니메이션(Idle/Run/Guard/Attack1/Attack2)을 재생한다. 공통
    /// 로직(컴포넌트 캐싱, 공격 예비동작~종료 라이프사이클, 좌우 반전)은
    /// UnitAnimationControllerBase가 담당하고, 여기서는 방패병 고유의 Guard 상태/1타 판정만
    /// 구현한다.
    ///
    /// CharacterMover.Target/StoppingDistance만으로 IsMoving/IsInRange를 계산한다("Guard"는
    /// Spearman의 "Defence"와 동일 역할 - 사거리 안에 들어오면 사용).
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
    /// 데미지는 사이클당 2회 들어간다 - Attack2가 끝나는 시점(Combat.Attacker의 정상적인
    /// AttackPerformed, Combat.MeleeAttackBehavior를 거쳐 이미 처리됨)과, Attack1이 끝나는
    /// 시점(attack1Duration 경과, 이 컨트롤러가 직접 처리) 둘 다 한 번씩. 후자는 Combat.Attacker의
    /// 정규 공격 주기와 무관한 별도 타격이라 War.Boss.WarBossPatternRunner/Combat.BearCharge의
    /// 돌진 명중 판정과 같은 방식(Health.TakeDamage 직접 호출, IAttackBehavior 사이클 밖)으로
    /// 처리한다. AttackPower는 사이클당 2회 타격을 반영해 기존의 절반으로 낮춰뒀다(DPS 유지).
    ///
    /// Guard 상태(사거리 안에 들어와 대기 중, 공격 중은 아님 - IsInRange&amp;&amp;!IsAttacking, 애니메이터가
    /// Guard로 전이하는 조건과 정확히 동일)인 동안 Character.Health.TakeDamage가 가장 먼저 적용하는
    /// Stats.DamageReductionPercent를 guardDamageReductionPercent만큼 얹어 받는 피해를 줄인다 -
    /// ShieldGuard의 별도 방패 체력 흡수보다도 앞선 단계라, 방패 체력 소모 자체도 함께 줄어든다.
    /// Guard를 벗어나는 즉시(공격 시작 등) 원래 값(BaseStats 기준)으로 되돌린다.
    ///
    /// 같은 오브젝트에 Character.ShieldGuard가 있고 그 방패가 이미 깨졌다면(HasShield==false),
    /// 사거리 안에 들어와도 Guard로 전이하지 않는다(방패 없이 브레이스 자세를 취하는 게 어색하고,
    /// 방패가 없는데 50% 피해 감소까지 받는 것도 앞뒤가 안 맞기 때문) - Idle/Run으로만 오간다.
    /// 다만 물리적으로는 이미 사거리 안에 멈춰있는 상태이므로(CharacterMover가 StoppingDistance
    /// 안에서 이동을 멈춤) 이 경우 IsMoving도 강제로 true를 만들지 않는다(제자리인데 Run이
    /// 재생되는 "제자리 뜀"을 피하기 위함) - 자연히 Idle로 떨어진다.
    /// </summary>
    public sealed class ShieldBearerAnimationController : UnitAnimationControllerBase
    {
        private static readonly int IsInRangeHash = Animator.StringToHash("IsInRange");

        /// <summary>
        /// Attack1 클립 실제 길이(초) - 이 시간이 지나면 Attack1 종료 타격을 직접 적용한다.
        /// Warrior_Attack1.anim(4프레임, 10fps)의 길이와 일치해야 한다.
        /// </summary>
        [SerializeField]
        private float attack1Duration = 0.4f;

        [SerializeField]
        private LayerMask enemyLayerMask;

        [SerializeField]
        private float guardDamageReductionPercent = 0.5f;

        private CharacterStatsProvider _statsProvider;
        private ShieldGuard _shieldGuard;

        private bool _firstHitDealt;
        private bool _isGuarding;

        protected override void Awake()
        {
            base.Awake();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _shieldGuard = GetComponent<ShieldGuard>();
        }

        protected override void OnEnable()
        {
            // 풀에서 재사용되는 인스턴스는 Awake가 다시 실행되지 않아 이전 생의 _isGuarding이
            // 그대로 남아있을 수 있다 - false로 리셋해, 스폰 직후 첫 Tick에서 실제 상태와 값이
            // 우연히 같아 보여도(둘 다 true) 반드시 한 번은 실제로 스탯을 적용하도록 한다.
            _isGuarding = false;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetGuarding(false);
        }

        protected override void OnAttackStarted()
        {
            _firstHitDealt = false;
        }

        protected override void OnAttackTick(float attackElapsed)
        {
            if (!_firstHitDealt && attackElapsed >= attack1Duration)
            {
                DealFirstHit();
                _firstHitDealt = true;
            }
        }

        protected override void TickMovement(float deltaTime)
        {
            bool hasTarget = Mover.Target != null;
            float distance = hasTarget ? Vector3.Distance(transform.position, Mover.Target.position) : 0f;

            // StoppingDistance는 실제 전투 사거리(EnemyTracker 등이 AttackRange로 설정)뿐 아니라
            // 순수 이동 목적(화면 복귀/후퇴/집결 등, 전부 0으로 설정 - Combat.RangedKiter 등 전체
            // 코드베이스에서 예외 없이 지켜지는 관례)으로도 재사용된다. 0이면 "지금 목표는 적이
            // 아니라 그냥 지나가는 지점"이라는 뜻이므로 Guard 판정에서 제외해야 한다 - 그렇지
            // 않으면 스테이지 시작 직후 화면 밖에서 복귀하는 도중(ScreenReturnAnchor, StoppingDistance
            // 0) 그 지점에 도착할 때마다 distance<=0이 참이 되어 근처에 적이 전혀 없는데도 Guard
            // 자세를 취하는 것처럼 보인다(실사용 중 발견).
            bool isPhysicallyInRange = hasTarget && Mover.StoppingDistance > 0f && distance <= Mover.StoppingDistance;
            bool hasShield = _shieldGuard == null || _shieldGuard.HasShield;
            bool isInRange = isPhysicallyInRange && hasShield;
            bool isMoving = hasTarget && !isPhysicallyInRange;

            Anim.SetBool(IsMovingHash, isMoving);
            Anim.SetBool(IsInRangeHash, isInRange);

            // 애니메이터가 Guard로 전이하는 조건(IsInRange && !IsAttacking)과 정확히 동일한
            // 조건으로 판정한다 - 별도로 GetCurrentAnimatorStateInfo를 읽지 않는 이유는, 상태
            // 전이 도중(크로스페이드)에는 그 값이 소스/목적지 어느 쪽을 가리킬지 모호해지기
            // 때문이다(실사용 중 확인). 여기서 쓰는 두 bool은 애니메이터에 넘기는 것과 완전히
            // 같은 값이라 항상 애니메이션과 정확히 일치한다.
            SetGuarding(isInRange && !IsAttacking);
        }

        private void SetGuarding(bool isGuarding)
        {
            if (isGuarding == _isGuarding)
            {
                return;
            }

            _isGuarding = isGuarding;

            float basePercent = _statsProvider.BaseStats.DamageReductionPercent;
            _statsProvider.Stats.DamageReductionPercent = isGuarding ? basePercent + guardDamageReductionPercent : basePercent;
        }

        /// <summary>
        /// Attack1이 끝나는 순간의 타격 - Combat.Attacker의 정규 공격 주기와 별개로, 이 컨트롤러가
        /// 직접 타겟을 찾아 데미지를 적용한다(War.Boss.WarBossPatternRunner/Combat.BearCharge의
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
    }
}
