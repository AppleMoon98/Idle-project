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
    ///
    /// AttackPerformed가 항상 뒤따라온다는 보장은 없다 - Combat.Attacker.Tick()은 예비동작 발행
    /// 이후 실제 발사 시점에 다시 한번 독립적으로 타겟을 찾는데, 그 사이 타겟이 죽거나 사거리를
    /// 벗어나면 AttackPerformed를 아예 발행하지 않고 조용히 다음 주기로 넘어간다(기존 Attacker
    /// 동작). IsShooting이 계속 true로 남으면 Shoot 클립(논루프)이 끝난 마지막 프레임에 영원히
    /// 멈춰버린다(실사용 중 발견 - Character.SpearmanAnimationController가 겪은 것과 같은 원인).
    /// maxShootHoldSeconds 타임아웃으로 방어한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class ArcherAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsShootingHash = Animator.StringToHash("IsShooting");

        [SerializeField]
        private float maxShootHoldSeconds = 1.2f;

        private Animator _animator;
        private CharacterMover _mover;
        private Attacker _attacker;

        private bool _isShooting;
        private float _shootElapsed;

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

            EndShoot();
        }

        private void OnWindupStarted(Health target)
        {
            _animator.SetBool(IsShootingHash, true);
            _isShooting = true;
            _shootElapsed = 0f;
        }

        private void OnAttackPerformed()
        {
            EndShoot();
        }

        private void EndShoot()
        {
            _animator.SetBool(IsShootingHash, false);
            _isShooting = false;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isShooting)
            {
                _shootElapsed += deltaTime;

                if (_shootElapsed >= maxShootHoldSeconds)
                {
                    EndShoot();
                }
            }

            bool isMoving = _mover.Target != null
                && Vector3.Distance(transform.position, _mover.Target.position) > _mover.StoppingDistance;

            _animator.SetBool(IsMovingHash, isMoving);
        }
    }
}
