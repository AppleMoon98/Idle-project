using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 창병 몸 스프라이트 시트 애니메이션(Idle/Run/Defence/Attack)을 재생한다. 공통 로직(컴포넌트
    /// 캐싱, 공격 예비동작~종료 라이프사이클, 좌우 반전)은 UnitAnimationControllerBase가 담당한다.
    ///
    /// CharacterMover.Target/StoppingDistance만으로 IsMoving/IsInRange를 계산한다 - EnemyTracker가
    /// 타겟을 잡을 때 StoppingDistance를 항상 AttackRange로 설정해두므로(Combat.EnemyTracker.Tick
    /// 참고), "Target이 있고 사거리보다 멀면 이동 중(Run)", "Target이 있고 사거리 이내면 대기
    /// 중(Defence)", "Target이 없으면 Idle"이라는 세 갈래가 그대로 성립한다.
    ///
    /// StoppingDistance는 실제 전투 사거리(EnemyTracker 등이 AttackRange로 설정)뿐 아니라 순수
    /// 이동 목적(화면 복귀/후퇴/집결 등, 전부 0으로 설정 - Combat.RangedKiter 등 전체 코드베이스에서
    /// 예외 없이 지켜지는 관례)으로도 재사용된다. 0이면 "지금 목표는 적이 아니라 그냥 지나가는
    /// 지점"이라는 뜻이므로 Defence 판정에서 제외해야 한다 - 그렇지 않으면 스테이지 시작 직후 화면
    /// 밖에서 복귀하는 도중(ScreenReturnAnchor, StoppingDistance 0) 그 지점에 도착할 때마다
    /// distance&lt;=0이 참이 되어 근처에 적이 전혀 없는데도 Defence 자세를 취하는 것처럼 보인다
    /// (Character.Animation.ShieldBearerAnimationController에서 실사용 중 먼저 발견된 것과 동일한
    /// 결함).
    /// </summary>
    public sealed class SpearmanAnimationController : UnitAnimationControllerBase
    {
        private static readonly int IsInRangeHash = Animator.StringToHash("IsInRange");

        protected override void TickMovement(float deltaTime)
        {
            bool hasTarget = Mover.Target != null;
            float distance = hasTarget ? Vector3.Distance(transform.position, Mover.Target.position) : 0f;

            bool isInRange = hasTarget && Mover.StoppingDistance > 0f && distance <= Mover.StoppingDistance;
            bool isMoving = hasTarget && !isInRange;

            Anim.SetBool(IsMovingHash, isMoving);
            Anim.SetBool(IsInRangeHash, isInRange);
        }
    }
}
