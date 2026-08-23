using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 기사 몸 스프라이트 시트 애니메이션(Idle/Run/Attack1)을 InfantryAnimationController와 동일한
    /// 신호(IsMoving/IsAttacking, UnitAnimationControllerBase가 공통 처리)로 재생한다. Attack2는
    /// 평상시 전투와 무관하게 예약된 상태다 - 지금은 병사 스킬 시스템 자체가 없어 아무도 호출하지
    /// 않지만, 나중에 생기면 PlaySkillCast() 한 번만 부르면 되도록 매커니즘만 미리 마련해둔다
    /// (ShieldBearerAnimationController처럼 매 공격마다 Attack1->Attack2를 자동으로 잇는 구조가
    /// 아니다 - 통상 공격은 Attack1만 재생하고 데미지도 Combat.Attacker의 정상 1회 사이클 그대로
    /// 유지된다).
    /// </summary>
    public sealed class KnightAnimationController : UnitAnimationControllerBase
    {
        private static readonly int CastSkillHash = Animator.StringToHash("CastSkill");

        [SerializeField]
        private bool pauseMovementDuringAttack = true;

        protected override bool PauseMovementDuringAttack => pauseMovementDuringAttack;

        /// <summary>
        /// Attack2(스킬 시전 모션)를 재생한다. 지금은 병사 스킬 시스템이 없어 호출하는 곳이 없다 -
        /// 나중에 생기면 그 시전 이벤트 핸들러에서 이 메서드만 부르면 된다.
        /// </summary>
        public void PlaySkillCast()
        {
            Anim.SetTrigger(CastSkillHash);
        }

        protected override void TickMovement(float deltaTime)
        {
            bool isMoving = Mover.Target != null
                && Vector3.Distance(transform.position, Mover.Target.position) > Mover.StoppingDistance;

            Anim.SetBool(IsMovingHash, isMoving);
        }
    }
}
