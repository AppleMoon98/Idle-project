using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Idle/Run 두 상태만 있는 몸 스프라이트 시트 애니메이션을 재생한다 - 공격하지 않는 캐릭터용
    /// (골드 던전의 파밍 대상 몬스터 등, Combat.Attacker 자체가 없다). InfantryAnimationController
    /// 등 공격 있는 유닛용 컨트롤러에서 IsAttacking 관련 로직만 뺀 것과 같은 모양 - CharacterMover.
    /// Target/StoppingDistance로 IsMoving을 구동하고, Target이 왼쪽에 있으면 SpriteRenderer.flipX로
    /// 좌우 반전한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CharacterMover))]
    public sealed class IdleRunAnimationController : MonoBehaviour, ITickable
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private Animator _animator;
        private CharacterMover _mover;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _mover = GetComponent<CharacterMover>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
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
