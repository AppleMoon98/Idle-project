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
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class SpearmanAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsInRangeHash = Animator.StringToHash("IsInRange");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

        private Animator _animator;
        private CharacterMover _mover;
        private Attacker _attacker;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _mover = GetComponent<CharacterMover>();
            _attacker = GetComponent<Attacker>();
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

            _animator.SetBool(IsAttackingHash, false);
        }

        private void OnWindupStarted(Health target)
        {
            _animator.SetBool(IsAttackingHash, true);
        }

        private void OnAttackPerformed()
        {
            _animator.SetBool(IsAttackingHash, false);
        }

        void ITickable.Tick(float deltaTime)
        {
            bool hasTarget = _mover.Target != null;
            float distance = hasTarget ? Vector3.Distance(transform.position, _mover.Target.position) : 0f;

            bool isMoving = hasTarget && distance > _mover.StoppingDistance;
            bool isInRange = hasTarget && distance <= _mover.StoppingDistance;

            _animator.SetBool(IsMovingHash, isMoving);
            _animator.SetBool(IsInRangeHash, isInRange);
        }
    }
}
