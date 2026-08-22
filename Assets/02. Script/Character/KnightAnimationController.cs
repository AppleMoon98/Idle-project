using Combat;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 기사 몸 스프라이트 시트 애니메이션(Idle/Run/Attack1)을 InfantryAnimationController와 동일한
    /// 신호(IsMoving/IsAttacking)로 재생한다. Attack2는 평상시 전투와 무관하게 예약된 상태다 -
    /// 지금은 병사 스킬 시스템 자체가 없어 아무도 호출하지 않지만, 나중에 생기면 PlaySkillCast()
    /// 한 번만 부르면 되도록 매커니즘만 미리 마련해둔다(ShieldBearerAnimationController처럼 매
    /// 공격마다 Attack1->Attack2를 자동으로 잇는 구조가 아니다 - 통상 공격은 Attack1만 재생하고
    /// 데미지도 Combat.Attacker의 정상 1회 사이클 그대로 유지된다).
    ///
    /// Attack1/Attack2 둘 다 Idle/Run 각각에서의 개별 전이로 애니메이터에 구성돼 있다(AnyState가
    /// 아님) - InfantryAnimationController/ShieldBearerAnimationController가 겪은 것과 같은 이유
    /// (AnyState 전이는 조건이 계속 참인 한 매 프레임 재발동한다).
    ///
    /// 공격 동안(IsAttacking) CharacterMover를 잠깐 꺼서 제자리에 멈춘다 - 같은 이유(Attacker의
    /// 독립적인 재탐색 타겟과 CharacterMover.Target이 다를 수 있어 "공격 자세인데 미끄러지듯
    /// 이동" 하는 문제 방지). OnDisable에서도 무조건 다시 켠다.
    ///
    /// AttackPerformed가 항상 뒤따라온다는 보장은 없다(Combat.Attacker.Tick() 참고) -
    /// maxAttackHoldSeconds 타임아웃으로 방어한다.
    ///
    /// 스프라이트 시트가 기본적으로 오른쪽을 보고 그려져 있어서, Target이 왼쪽에 있으면
    /// SpriteRenderer.flipX로 좌우 반전한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class KnightAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int CastSkillHash = Animator.StringToHash("CastSkill");

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

        /// <summary>
        /// Attack2(스킬 시전 모션)를 재생한다. 지금은 병사 스킬 시스템이 없어 호출하는 곳이 없다 -
        /// 나중에 생기면 그 시전 이벤트 핸들러에서 이 메서드만 부르면 된다.
        /// </summary>
        public void PlaySkillCast()
        {
            _animator.SetTrigger(CastSkillHash);
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
