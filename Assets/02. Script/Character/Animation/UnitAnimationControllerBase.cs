using Character;
using Combat;
using Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Character.Animation
{
    /// <summary>
    /// 병종별 몸 스프라이트 시트 애니메이션 컨트롤러(Infantry/Knight/Archer/Spearman/ShieldBearer/
    /// Bear/IdleRun) 7종이 공통으로 갖던 로직을 추출한 베이스 클래스. 컴포넌트 캐싱(Awake),
    /// GameTicker 등록/해제와 Attacker 이벤트 구독/해제(OnEnable/OnDisable), 공격 예비동작~종료
    /// 라이프사이클(OnWindupStarted/EndAttack)과 그 타임아웃 감시(maxAttackHoldSeconds), 기본 좌우
    /// 반전(UpdateFacing)은 7종 전부(또는 공격이 있는 6종) 사이에서 완전히 동일했다 - 각 파생
    /// 클래스는 "이동 중인지(TickMovement)"를 계산하는 병종 고유 공식과, 필요하면 몇 가지 훅
    /// (OnAttackStarted/OnAttackTick/UpdateFacing/PauseMovementDuringAttack/AttackAnimatorBoolHash)
    /// 만 오버라이드한다.
    ///
    /// AttackPerformed가 항상 뒤따라온다는 보장은 없다(Combat.Attacker.Tick() 참고 - 예비동작 이후
    /// 실제 발사 시점에 독립적으로 다시 타겟을 찾는데, 그 사이 타겟이 죽거나 사거리를 벗어나면
    /// AttackPerformed 없이 조용히 다음 주기로 넘어간다) - maxAttackHoldSeconds 타임아웃이 그 경우를
    /// 방어한다(실사용 중 여러 병종에서 반복 발견됐던 문제).
    ///
    /// 필드명(maxAttackHoldSeconds)은 이미 여러 프리팹에 직렬화돼 있던 것과 동일하게 유지했다 -
    /// Unity는 SerializeField를 타입 계층과 무관하게 이름으로 바인딩하므로, 이 필드를 파생 클래스에서
    /// 베이스로 옮겨도 기존 프리팹의 값은 그대로 보존된다. Archer만 원래 필드명이
    /// maxShootHoldSeconds였어서 FormerlySerializedAs로 옛 이름의 직렬화 값을 그대로 이어받는다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public abstract class UnitAnimationControllerBase : MonoBehaviour, ITickable
    {
        protected static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        protected static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        [FormerlySerializedAs("maxShootHoldSeconds")]
        [SerializeField]
        private float maxAttackHoldSeconds = 0.6f;

        protected Animator Anim { get; private set; }
        protected CharacterMover Mover { get; private set; }
        protected Attacker AttackerComponent { get; private set; }
        protected SpriteRenderer SpriteRendererComponent { get; private set; }

        protected bool IsAttacking { get; private set; }

        private float _attackElapsed;

        /// <summary>공격 중 CharacterMover를 잠깐 꺼서 제자리에 멈출지. 기본값 true(대다수 병종의
        /// 기존 하드코딩 동작) - Infantry/Knight만 인스펙터 토글로 노출하고, Archer는 항상 false로
        /// 고정한다.</summary>
        protected virtual bool PauseMovementDuringAttack => true;

        /// <summary>공격 진행 여부를 나타내는 애니메이터 bool 파라미터. 기본값 "IsAttacking" -
        /// Archer만 "IsShooting"으로 다르다(의미는 동일).</summary>
        protected virtual int AttackAnimatorBoolHash => IsAttackingHash;

        protected virtual void Awake()
        {
            Anim = GetComponent<Animator>();
            Mover = GetComponent<CharacterMover>();
            AttackerComponent = GetComponent<Attacker>();
            SpriteRendererComponent = GetComponent<SpriteRenderer>();
        }

        protected virtual void OnEnable()
        {
            TickerRegistration.Register(this);

            if (AttackerComponent != null)
            {
                AttackerComponent.AttackWindupStarted += OnWindupStarted;
                AttackerComponent.AttackPerformed += OnAttackPerformed;
            }
        }

        protected virtual void OnDisable()
        {
            TickerRegistration.Unregister(this);

            if (AttackerComponent != null)
            {
                AttackerComponent.AttackWindupStarted -= OnWindupStarted;
                AttackerComponent.AttackPerformed -= OnAttackPerformed;
                EndAttack();
            }
        }

        private void OnWindupStarted(Health target)
        {
            Anim.SetBool(AttackAnimatorBoolHash, true);

            if (PauseMovementDuringAttack)
            {
                Mover.enabled = false;
            }

            IsAttacking = true;
            _attackElapsed = 0f;
            OnAttackStarted();
        }

        private void OnAttackPerformed()
        {
            EndAttack();
        }

        protected virtual void EndAttack()
        {
            Anim.SetBool(AttackAnimatorBoolHash, false);

            if (PauseMovementDuringAttack)
            {
                Mover.enabled = true;
            }

            IsAttacking = false;
        }

        /// <summary>공격 예비동작이 시작되는 순간(윈드업) 파생 클래스가 추가로 초기화할 상태가
        /// 있으면 override(예: ShieldBearer의 1타 판정 플래그 리셋).</summary>
        protected virtual void OnAttackStarted()
        {
        }

        /// <summary>공격 진행 중(IsAttacking) 매 틱, 경과 시간과 함께 호출된다. 타임아웃 판정보다
        /// 먼저 호출되므로 그 안에서 EndAttack이 발동하기 전에 처리해야 할 것(예: ShieldBearer의
        /// Attack1 종료 타격)을 넣을 수 있다.</summary>
        protected virtual void OnAttackTick(float attackElapsed)
        {
        }

        void ITickable.Tick(float deltaTime)
        {
            if (IsAttacking)
            {
                _attackElapsed += deltaTime;
                OnAttackTick(_attackElapsed);

                if (_attackElapsed >= maxAttackHoldSeconds)
                {
                    EndAttack();
                }
            }

            TickMovement(deltaTime);
            UpdateFacing();
        }

        /// <summary>이동/사거리 판정은 병종마다 공식이 달라(단순 이동 판정 vs 사거리 안 대기까지
        /// 구분) 파생 클래스가 직접 구현한다 - Animator의 IsMoving(및 필요하면 IsInRange 등) bool을
        /// 여기서 SetBool한다.</summary>
        protected abstract void TickMovement(float deltaTime);

        /// <summary>스프라이트 시트가 기본적으로 오른쪽을 보고 그려져 있어서, Target이 왼쪽에 있으면
        /// 좌우 반전한다. Bear만 돌진 방향 기준으로 오버라이드한다.</summary>
        protected virtual void UpdateFacing()
        {
            if (Mover.Target == null)
            {
                return;
            }

            float dx = Mover.Target.position.x - transform.position.x;

            if (Mathf.Abs(dx) > 0.01f)
            {
                SpriteRendererComponent.flipX = dx < 0f;
            }
        }
    }
}
