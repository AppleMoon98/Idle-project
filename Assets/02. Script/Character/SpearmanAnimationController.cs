using Combat;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 창병 몸 스프라이트 시트 애니메이션(Idle/Run/Defence/Attack)을 기존 신호만으로 재생한다 -
    /// ArcherAnimationController와 같은 방향. CharacterMover.Target/StoppingDistance만으로
    /// IsMoving/IsInRange를 계산한다 - EnemyTracker가 타겟을 잡을 때 StoppingDistance를 항상
    /// AttackRange로 설정해두므로(Combat.EnemyTracker.Tick 참고), "Target이 있고 사거리보다 멀면
    /// 이동 중(Run)", "Target이 있고 사거리 이내면 대기 중(Defence)", "Target이 없으면 Idle"이라는
    /// 세 갈래가 그대로 성립한다. IsAttacking은 같은 오브젝트의 Attacker가 발행하는
    /// AttackWindupStarted/AttackPerformed로 구동한다(EventBus 아님 - 같은 캐릭터 위 컴포넌트
    /// 간 직접 알림, RangedAttackTelegraph와 동일 패턴).
    ///
    /// 찌르기 동안(IsAttacking) CharacterMover를 잠깐 꺼서 제자리에 멈춘다 - Attacker는 자기 사거리
    /// 안에서 독립적으로 최근접 대상을 찾아 공격하는데, 주변에 적이 여럿이면 그 대상이
    /// CharacterMover.Target(EnemyTracker/FormationFollower가 잡은, 아직 접근 중일 수 있는 다른
    /// 대상)과 다를 수 있다 - 그러면 Attack 애니메이션이 재생되는 동안에도 CharacterMover가 계속
    /// 그 다른 대상 쪽으로 걸어가 버려 "찌르는 자세인데 미끄러지듯 이동하는" 것처럼 보인다(실사용
    /// 중 발견). OnDisable에서도 무조건 다시 켜서(죽음/스테이지 전환 등으로 공격 도중 비활성화돼도)
    /// 풀에서 재사용될 때 영구히 멈춰있는 상태로 남지 않게 한다(Character.KnockbackReceiver가
    /// 같은 이유로 OnDisable에서 항상 복구하는 것과 동일한 안전장치).
    ///
    /// AttackPerformed가 항상 뒤따라온다는 보장은 없다 - Combat.Attacker.Tick()은 예비동작
    /// 발행 이후 실제 발사 시점에 다시 한번 독립적으로 타겟을 찾는데, 그 사이 타겟이 죽거나
    /// 사거리를 벗어나면 AttackPerformed를 아예 발행하지 않고 조용히 다음 주기로 넘어간다(기존
    /// Attacker 동작, 이 컴포넌트가 생기기 전엔 애니메이션만 어색하게 멈추고 끝나는 정도였지만,
    /// CharacterMover까지 꺼버리는 지금은 그대로 두면 영구 정지로 이어진다 - 실사용 중 발견).
    /// maxAttackHoldSeconds 타임아웃으로 이 경우를 방어한다.
    ///
    /// 스프라이트 시트가 기본적으로 오른쪽을 보고 그려져 있어서, Target이 왼쪽에 있으면
    /// SpriteRenderer.flipX로 좌우 반전한다. Target이 없는 동안은 마지막으로 바라보던 방향을
    /// 그대로 유지한다(매 틱 되돌릴 근거가 없다). CharacterMover가 꺼져 있는 동안(찌르기 중)에도
    /// Target 값 자체는 그대로 남아있어 방향은 계속 정확하게 갱신된다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class SpearmanAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsInRangeHash = Animator.StringToHash("IsInRange");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        [SerializeField]
        private float maxAttackHoldSeconds = 0.6f;

        private Animator _animator;
        private CharacterMover _mover;
        private Attacker _attacker;
        private SpriteRenderer _spriteRenderer;

        private bool _isAttacking;
        private float _attackElapsed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _mover = GetComponent<CharacterMover>();
            _attacker = GetComponent<Attacker>();
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

                if (_attackElapsed >= maxAttackHoldSeconds)
                {
                    EndAttack();
                }
            }

            bool hasTarget = _mover.Target != null;
            float distance = hasTarget ? Vector3.Distance(transform.position, _mover.Target.position) : 0f;

            bool isMoving = hasTarget && distance > _mover.StoppingDistance;
            bool isInRange = hasTarget && distance <= _mover.StoppingDistance;

            _animator.SetBool(IsMovingHash, isMoving);
            _animator.SetBool(IsInRangeHash, isInRange);
            UpdateFacing();
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
