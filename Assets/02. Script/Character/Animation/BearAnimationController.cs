using Combat;
using UnityEngine;

namespace Character.Animation
{
    /// <summary>
    /// 기마병(곰) 몸 스프라이트 시트 애니메이션(Idle/Run/Attack)을 재생한다. 공통 로직(컴포넌트
    /// 캐싱, 공격 예비동작~종료 라이프사이클)은 UnitAnimationControllerBase가 담당한다.
    ///
    /// Combat.BearCharge가 돌진(Charging) 중에는 CharacterMover를 거치지 않고 직접 transform을
    /// 이동시키므로, 다른 병종이 쓰는 "Target != null &amp;&amp; 거리 > StoppingDistance" 판정만으로는
    /// 돌진을 감지할 수 없다 - BearCharge.IsCharging을 직접 읽어 그 경우도 Run으로 잡는다(TickMovement
    /// 오버라이드). 근접 교전 거리(chargeStartDistance보다 가까울 때)에서는 BearCharge가
    /// CharacterMover.Target을 위협으로 세팅해두므로, 그 경우는 기존 판정 그대로 다가가는 동안 Run,
    /// 사거리 안에 서면 Idle로 떨어진다.
    ///
    /// 좌우 반전(UpdateFacing)도 오버라이드한다 - 돌진 중에는 CharacterMover.Target이 없어(직접
    /// transform 이동) 기본 구현(Target 기준)이 아무 근거가 없으므로, BearCharge.ChargeDirection을
    /// 대신 읽는다.
    /// </summary>
    [RequireComponent(typeof(BearCharge))]
    public sealed class BearAnimationController : UnitAnimationControllerBase
    {
        private BearCharge _bearCharge;

        protected override void Awake()
        {
            base.Awake();
            _bearCharge = GetComponent<BearCharge>();
        }

        protected override void TickMovement(float deltaTime)
        {
            bool isMoving = _bearCharge.IsCharging
                || (Mover.Target != null && Vector3.Distance(transform.position, Mover.Target.position) > Mover.StoppingDistance);

            Anim.SetBool(IsMovingHash, isMoving);
        }

        protected override void UpdateFacing()
        {
            float dx;

            if (_bearCharge.IsCharging)
            {
                dx = _bearCharge.ChargeDirection.x;
            }
            else if (Mover.Target != null)
            {
                dx = Mover.Target.position.x - transform.position.x;
            }
            else
            {
                return;
            }

            if (Mathf.Abs(dx) > 0.01f)
            {
                SpriteRendererComponent.flipX = dx < 0f;
            }
        }
    }
}
