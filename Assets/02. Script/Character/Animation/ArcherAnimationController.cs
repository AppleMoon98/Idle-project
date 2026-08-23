using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 아처(궁병) 몸 스프라이트 시트 애니메이션(Idle/Run/Shoot)을 재생한다. 공통 로직(컴포넌트 캐싱,
    /// 공격 예비동작~종료 라이프사이클, 좌우 반전)은 UnitAnimationControllerBase가 담당한다 -
    /// 다만 애니메이터의 공격 상태 bool 이름이 다른 병종의 "IsAttacking"과 달리 "IsShooting"이라
    /// AttackAnimatorBoolHash를 오버라이드하고, 활을 쏘는 동안 CharacterMover를 멈추지 않는다
    /// (PauseMovementDuringAttack=false로 고정).
    /// </summary>
    public sealed class ArcherAnimationController : UnitAnimationControllerBase
    {
        private static readonly int IsShootingHash = Animator.StringToHash("IsShooting");

        protected override bool PauseMovementDuringAttack => false;

        protected override int AttackAnimatorBoolHash => IsShootingHash;

        protected override void TickMovement(float deltaTime)
        {
            bool isMoving = Mover.Target != null
                && Vector3.Distance(transform.position, Mover.Target.position) > Mover.StoppingDistance;

            Anim.SetBool(IsMovingHash, isMoving);
        }
    }
}
