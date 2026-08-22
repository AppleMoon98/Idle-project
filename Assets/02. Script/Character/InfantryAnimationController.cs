using Combat;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 보병 몸 스프라이트 시트 애니메이션(Idle/Run/Attack)을 기존 신호만으로 재생한다 -
    /// ArcherAnimationController/SpearmanAnimationController와 같은 방향. CharacterMover.Target/
    /// StoppingDistance로 IsMoving을, 같은 오브젝트의 Attacker가 발행하는
    /// AttackWindupStarted/AttackPerformed로 IsAttacking을 구동한다.
    ///
    /// Attack 진입은 AnyState가 아니라 Idle/Run 각각에서 Attack로 가는 개별 전이(조건
    /// IsAttacking==true)로 만들었다 - ShieldBearerAnimationController에서 겪은 것과 같은 이유
    /// (AnyState 전이는 조건이 계속 참인 한 매 프레임 재발동해 클립이 반복 재시작된다). 개별
    /// 전이는 그 소스 상태에 있을 때만 평가되므로 Attack에 들어간 순간부터는 재검사 대상이
    /// 아니라 이 문제 자체가 구조적으로 발생할 수 없다.
    ///
    /// 공격 동안(IsAttacking) CharacterMover를 잠깐 꺼서 제자리에 멈춘다 - SpearmanAnimationController
    /// 가 겪은 것과 같은 이유(Attacker의 독립적인 재탐색 타겟과 CharacterMover.Target이 다를 수 있어
    /// "공격 자세인데 미끄러지듯 이동" 하는 문제 방지). OnDisable에서도 무조건 다시 켠다.
    /// pauseMovementDuringAttack(기본 true)을 끄면 이 정지 자체를 하지 않는다 - 플레이어는 공격
    /// 중에도 이동이 끊기지 않아야 한다는 요청으로 추가됐다(Player 인스턴스에서만 false, Monster/
    /// Soldier 프리팹은 이 컴포넌트를 그대로 공유하므로 기본값 유지).
    ///
    /// AttackPerformed가 항상 뒤따라온다는 보장은 없다(Combat.Attacker.Tick() 참고) -
    /// maxAttackHoldSeconds 타임아웃으로 방어한다.
    ///
    /// 스프라이트 시트가 기본적으로 오른쪽을 보고 그려져 있어서, Target이 왼쪽에 있으면
    /// SpriteRenderer.flipX로 좌우 반전한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class InfantryAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        [SerializeField]
        private float maxAttackHoldSeconds = 0.6f;

        [SerializeField]
        private bool pauseMovementDuringAttack = true;

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

            if (pauseMovementDuringAttack)
            {
                _mover.enabled = false;
            }

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

            if (pauseMovementDuringAttack)
            {
                _mover.enabled = true;
            }

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

            bool isMoving = _mover.Target != null
                && Vector3.Distance(transform.position, _mover.Target.position) > _mover.StoppingDistance;

            _animator.SetBool(IsMovingHash, isMoving);
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
