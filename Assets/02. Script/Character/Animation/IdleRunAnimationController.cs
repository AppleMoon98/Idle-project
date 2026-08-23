using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// Idle/Run 두 상태만 있는 몸 스프라이트 시트 애니메이션을 재생한다 - 공격하지 않는 캐릭터용
    /// (골드 던전의 파밍 대상 몬스터 등, Combat.Attacker 자체가 없다). UnitAnimationControllerBase의
    /// AttackerComponent가 null인 채로 자연히 공격 관련 로직 전체를 건너뛰므로, 여기서는 이동 판정만
    /// 구현하면 된다.
    /// </summary>
    public sealed class IdleRunAnimationController : UnitAnimationControllerBase
    {
        protected override void TickMovement(float deltaTime)
        {
            bool isMoving = Mover.Target != null
                && Vector3.Distance(transform.position, Mover.Target.position) > Mover.StoppingDistance;

            Anim.SetBool(IsMovingHash, isMoving);
        }
    }
}
