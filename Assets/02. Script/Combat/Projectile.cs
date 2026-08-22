using Character;
using Core;
using Managers;
using Services;
using Stage.Events;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 발사 시점 조준 방향으로 직선으로 날아가는 발사체. Launch() 시점에 방향만 한 번 고정하고
    /// (호밍 없음), 그 뒤로는 목적지도 특정 타겟 참조도 없이 그 방향으로 계속 날아간다 - "예고선은
    /// 방향만 잡아주고, 발사된 이후로는 그 방향으로 그냥 쭉 날아가는 단일 오브젝트"라는 설계.
    /// 발사체는 데미지 정보만 들고 있으면 되고, 명중 판정은 매 틱 이번 프레임에 실제로 지나온
    /// 경로를 targetLayerMask 기준으로 Physics2D.CircleCast(터널링 방지 - 프레임이 조금만 굵어져도
    /// 얇은 판정을 그대로 관통하는 걸 막는다, 모바일 프레임 드랍 고려)해 스스로 판단한다 - 원래
    /// 조준했던 대상이 아니어도 이 레이어의 살아있는 Health를 처음 스친 대상이면 그 자리에서
    /// 맞고 소멸하고, 레이어에 안 걸리는 대상(적이 아닌 것)은 그냥 통과해 계속 날아간다. 끝내
    /// 아무것도 못 맞히면 최광각 고정 범위(Services.CameraFollowService, 줌 배율과 무관 — section
    /// CD/CG/CH와 동일 원칙) 밖으로 나가는 순간 반납된다 - 화살이 허공에서 뚝 끊기지 않고 자연스럽게
    /// 화면 밖으로 날아가 보이게 하기 위함. Stage.Events.StageChangedEvent(진행/반복/사망 후퇴 전부)를
    /// 직접 구독해, 명중 여부와 무관하게 스테이지가 바뀌는 순간 무조건 스스로 반납한다 — 몬스터/
    /// 병사 쪽이 이미 쓰는 "스테이지 경계 = 완전 초기화" 관례와 동일하다.
    /// </summary>
    public sealed class Projectile : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float speed = 10f;

        /// <summary>
        /// 명중 판정에 쓰는 원형 스윕의 반지름 - 발사체 자체의 "두께" 개념.
        /// </summary>
        [SerializeField]
        private float hitRadius = 0.2f;

        /// <summary>
        /// 켜면 매 틱 이동 방향으로 transform을 회전시킨다(예: 화살처럼 방향성이 뚜렷한 스프라이트).
        /// 기본값 false라 기존에 방향과 무관하게 보여도 되는 발사체(원형 등)는 전혀 영향받지 않는다.
        /// </summary>
        [SerializeField]
        private bool rotateToFaceDirection = false;

        private float _damage;
        private bool _isCritical;
        private LayerMask _hitLayerMask;
        private bool _released;
        private Vector3 _direction;
        private CameraFollowService _cameraFollowService;

        private void OnEnable()
        {
            _released = false;
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            ReleaseSelf();
        }

        /// <summary>
        /// 발사체를 발사한다. 풀에서 꺼낸 직후 호출되어야 한다. aimTarget은 발사 순간의 조준
        /// 방향을 정하는 데만 쓰이고(그 위치를 향하는 방향을 한 번 계산한 뒤 버림), 이후로는
        /// 전혀 참조하지 않는다 - 명중 판정은 hitLayerMask에 걸리는 아무 대상이나 대상으로 삼는다.
        /// </summary>
        public void Launch(Health aimTarget, float damage, bool isCritical, LayerMask hitLayerMask)
        {
            _damage = damage;
            _isCritical = isCritical;
            _hitLayerMask = hitLayerMask;

            Vector3 aimPoint = aimTarget != null ? aimTarget.transform.position : transform.position;
            Vector3 direction = aimPoint - transform.position;
            _direction = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : transform.right;

            if (rotateToFaceDirection)
            {
                float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            float distance = speed * deltaTime;
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, hitRadius, _direction, distance, _hitLayerMask);

            if (hit.collider != null && hit.collider.TryGetComponent(out Health health) && !health.IsDead)
            {
                health.TakeDamage(_damage, _isCritical);
                ReleaseSelf();
                return;
            }

            transform.position += _direction * distance;

            if (_cameraFollowService != null
                && !CameraVisibility.IsWithinBounds(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), transform.position))
            {
                ReleaseSelf();
            }
        }

        /// <summary>
        /// 명중이 곧바로 스테이지 클리어→전환으로 이어지면(예: 마지막 몬스터를 죽인 발사체),
        /// TakeDamage 안에서 StageChangedEvent가 동기적으로 발행돼 OnStageChanged가 먼저
        /// ReleaseSelf를 부르고, Tick의 나머지 코드가 이어서 또 한 번 부르는 이중 반납이 같은
        /// 프레임 안에서 일어날 수 있다 — 풀 스택에 같은 인스턴스가 두 번 들어가면 이후 서로
        /// 다른 두 호출자가 같은 GameObject를 동시에 "새로" 받는 심각한 오염으로 이어지므로,
        /// 한 번만 실제로 반납되도록 막는다.
        /// </summary>
        private void ReleaseSelf()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(gameObject);
            }
        }
    }
}
