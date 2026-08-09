using Core;
using Services;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 플레이어가 Services.CameraFollowService의 경계(줌 슬라이더 value=0일 때 보이는 사각형) 밖으로
    /// 나가지 못하게 매 틱 위치를 클램프한다. Character.CharacterSeparation(섹션 BO)과 같은 방식의
    /// additive 컴포넌트 — CharacterMover/PlayerManualMover가 그 프레임에 무엇을 했든 상관없이
    /// 사후에 위치만 보정하며, 이동 로직 자체는 전혀 건드리지 않는다.
    /// </summary>
    public sealed class PlayerWorldBoundsConstraint : MonoBehaviour, ITickable
    {
        private CameraFollowService _followService;

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
            if (_followService == null
                && !(GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out _followService)))
            {
                return;
            }

            Vector3 home = _followService.HomeLocalPosition;
            Vector2 halfExtent = _followService.GetWorldBoundsHalfExtent();

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, home.x - halfExtent.x, home.x + halfExtent.x);
            position.y = Mathf.Clamp(position.y, home.y - halfExtent.y, home.y + halfExtent.y);
            transform.position = position;
        }
    }
}
