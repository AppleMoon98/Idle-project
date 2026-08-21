using Combat;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 아처(궁병) 몸 스프라이트 시트 애니메이션(Idle/Run/Shoot)을 기존 신호만으로 재생한다 - 새
    /// 게임플레이 로직을 추가하지 않고, Combat.MonsterMarchingTracker가 이미 쓰는 것과 동일한
    /// "Target까지 거리가 StoppingDistance보다 크면 이동 중" 공식으로 IsMoving을, 같은 오브젝트의
    /// Combat.Attacker가 이미 발행하는 AttackWindupStarted/AttackPerformed(RangedAttackTelegraph가
    /// 예고선에 쓰는 것과 동일한 이벤트, EventBus 아님 - 같은 캐릭터 위 컴포넌트 간 직접 알림)로
    /// IsShooting을 구동한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class ArcherAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsShootingHash = Animator.StringToHash("IsShooting");

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

            _animator.SetBool(IsShootingHash, false);
        }

        private void OnWindupStarted(Health target)
        {
            _animator.SetBool(IsShootingHash, true);
        }

        private void OnAttackPerformed()
        {
            _animator.SetBool(IsShootingHash, false);
        }

        void ITickable.Tick(float deltaTime)
        {
            bool isMoving = _mover.Target != null
                && Vector3.Distance(transform.position, _mover.Target.position) > _mover.StoppingDistance;

            _animator.SetBool(IsMovingHash, isMoving);
        }
    }
}
