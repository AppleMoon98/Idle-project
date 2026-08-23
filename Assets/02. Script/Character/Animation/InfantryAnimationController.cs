using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 보병 몸 스프라이트 시트 애니메이션(Idle/Run/Attack)을 재생한다. 공통 로직(컴포넌트 캐싱,
    /// 공격 예비동작~종료 라이프사이클, 좌우 반전)은 UnitAnimationControllerBase가 담당하고, 여기서는
    /// "이동 중인지"만 CharacterMover.Target/StoppingDistance로 계산한다.
    ///
    /// pauseMovementDuringAttack(기본 true)을 끄면 공격 중에도 CharacterMover를 멈추지 않는다 -
    /// 플레이어는 공격 중에도 이동이 끊기지 않아야 한다는 요청으로 추가됐다(Player 인스턴스에서만
    /// false, Monster/Soldier 프리팹은 이 컴포넌트를 그대로 공유하므로 기본값 유지).
    /// </summary>
    public sealed class InfantryAnimationController : UnitAnimationControllerBase
    {
        [SerializeField]
        private bool pauseMovementDuringAttack = true;

        protected override bool PauseMovementDuringAttack => pauseMovementDuringAttack;

        protected override void TickMovement(float deltaTime)
        {
            bool isMoving = Mover.Target != null
                && Vector3.Distance(transform.position, Mover.Target.position) > Mover.StoppingDistance;

            Anim.SetBool(IsMovingHash, isMoving);
        }
    }
}
