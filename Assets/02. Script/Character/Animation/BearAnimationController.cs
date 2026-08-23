using Character;
using Combat;
using Core;
using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 기마병(곰) 몸 스프라이트 시트 애니메이션(Idle/Run/Attack)을 재생한다. Combat.BearCharge가
    /// 돌진(Charging) 중에는 CharacterMover를 거치지 않고 직접 transform을 이동시키므로,
    /// InfantryAnimationController류가 쓰는 "Target != null && 거리 > StoppingDistance" 판정만으로는
    /// 돌진을 감지할 수 없다 - BearCharge.IsCharging을 직접 읽어 그 경우도 Run으로 잡는다.
    /// 근접 교전 거리(chargeStartDistance보다 가까울 때)에서는 BearCharge가 CharacterMover.Target을
    /// 위협으로 세팅해두므로, 그 경우는 기존 판정 그대로 다가가는 동안 Run, 사거리 안에 서면 Idle로
    /// 떨어진다.
    ///
    /// IsAttacking은 같은 오브젝트의 Attacker가 발행하는 AttackWindupStarted/AttackPerformed로
    /// 구동한다(InfantryAnimationController와 동일한 패턴) - 공격은 근접 교전 중에만 실제로
    /// 발동하지만(사거리 판정은 Attacker 자신이 독립적으로 함), 이 컨트롤러는 그 사실을 몰라도 된다.
    ///
    /// 공격 동안 CharacterMover를 잠깐 꺼서 제자리에 멈춘다 - Attacker의 독립적인 재탐색 타겟과
    /// CharacterMover.Target이 다를 수 있어 "공격 자세인데 미끄러지듯 이동" 하는 문제 방지(다른
    /// *AnimationController들과 동일한 이유). 돌진 중에는 애초에 CharacterMover가 비활성 개념이
    /// 아니라 아예 안 쓰이므로(Target=null) 이 처리와 충돌하지 않는다.
    ///
    /// 스프라이트 시트가 기본적으로 오른쪽을 보고 그려져 있어서, 이동/조준 방향이 왼쪽이면
    /// SpriteRenderer.flipX로 좌우 반전한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(BearCharge))]
    public sealed class BearAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        [SerializeField]
        private float maxAttackHoldSeconds = 1.2f;

        private Animator _animator;
        private CharacterMover _mover;
        private Attacker _attacker;
        private BearCharge _bearCharge;
        private SpriteRenderer _spriteRenderer;

        private bool _isAttacking;
        private float _attackElapsed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _mover = GetComponent<CharacterMover>();
            _attacker = GetComponent<Attacker>();
            _bearCharge = GetComponent<BearCharge>();
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

            bool isMoving = _bearCharge.IsCharging
                || (_mover.Target != null && Vector3.Distance(transform.position, _mover.Target.position) > _mover.StoppingDistance);

            _animator.SetBool(IsMovingHash, isMoving);
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            float dx;

            if (_bearCharge.IsCharging)
            {
                dx = _bearCharge.ChargeDirection.x;
            }
            else if (_mover.Target != null)
            {
                dx = _mover.Target.position.x - transform.position.x;
            }
            else
            {
                return;
            }

            if (Mathf.Abs(dx) > 0.01f)
            {
                _spriteRenderer.flipX = dx < 0f;
            }
        }
    }
}
