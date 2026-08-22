using Character;
using Core;
using Managers;
using Services;
using Stage.Events;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 발사 시점 타겟 위치를 향해 직선으로 날아가다가 도달하면 데미지를 적용하고 스스로 풀로
    /// 반납되는 발사체. 목적지는 Launch() 시점에 한 번만 고정되며(호밍 없음), 비행 중 타겟이
    /// 움직여도 경로가 휘지 않는다 — 도달 판정 시 데미지는 여전히 원래 타겟(Health 참조)에게
    /// 적용된다. 타겟이 비행 중 죽으면(Health.IsDead) 그 자리에서 즉시 사라지지 않고, 그 순간까지
    /// 날아가던 방향 그대로 계속 직진하다가 최광각 고정 범위(Services.CameraFollowService, 줌
    /// 배율과 무관 — 이 프로젝트 전역의 "판정 기준은 줌과 무관한 고정 범위" 관례, section CD/CG/CH와
    /// 동일 원칙) 밖으로 나가는 순간 반납된다 — 화살이 죽은 타겟 자리에서 뚝 끊기지 않고 자연스럽게
    /// 화면 밖으로 날아가 보이게 하기 위함. Character.Health.Die가
    /// CharacterDiedEvent를 발행하면 스테이지 전환(플레이어 사망 → 스테이지 재시작 → 그 안에서
    /// Health.Revive)까지 전부 같은 호출 안에서 동기적으로 끝나버려, 그 사이 다른 발사체가 다음
    /// 틱에 IsDead를 확인할 때는 이미 부활해 false로 돌아가 있다(타겟이 죽었었다는 걸 영영 감지
    /// 못함). 몬스터 쪽도 Stage.StageProgressTracker.ReleaseRemaining이 남은 몬스터를 죽음 처리
    /// 없이 그냥 풀로 반환하면 마찬가지다. 그래서 Stage.Events.StageChangedEvent(진행/반복/사망
    /// 후퇴 전부)를 직접 구독해, 타겟 상태와 무관하게 스테이지가 바뀌는 순간 무조건 스스로
    /// 반납한다 — 몬스터/병사 쪽이 이미 쓰는 "스테이지 경계 = 완전 초기화" 관례와 동일하다.
    /// </summary>
    public sealed class Projectile : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float speed = 10f;

        [SerializeField]
        private float hitDistance = 0.2f;

        /// <summary>
        /// 켜면 매 틱 이동 방향으로 transform을 회전시킨다(예: 화살처럼 방향성이 뚜렷한 스프라이트).
        /// 기본값 false라 기존에 방향과 무관하게 보여도 되는 발사체(원형 등)는 전혀 영향받지 않는다.
        /// </summary>
        [SerializeField]
        private bool rotateToFaceDirection = false;

        private Health _target;
        private float _damage;
        private bool _isCritical;
        private bool _released;
        private Vector3 _destination;
        private Vector3 _direction;
        private bool _targetLostMidFlight;
        private CameraFollowService _cameraFollowService;

        private void OnEnable()
        {
            _released = false;
            _targetLostMidFlight = false;
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
        /// 발사체를 발사한다. 풀에서 꺼낸 직후 호출되어야 한다.
        /// </summary>
        public void Launch(Health target, float damage, bool isCritical)
        {
            _target = target;
            _damage = damage;
            _isCritical = isCritical;
            _destination = target != null ? target.transform.position : transform.position;
            _targetLostMidFlight = false;

            Vector3 direction = _destination - transform.position;
            _direction = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : transform.right;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_targetLostMidFlight)
            {
                TickFlyingPastDeadTarget(deltaTime);
                return;
            }

            if (_target == null || _target.IsDead)
            {
                _targetLostMidFlight = true;
                TickFlyingPastDeadTarget(deltaTime);
                return;
            }

            if (rotateToFaceDirection)
            {
                Vector3 direction = _destination - transform.position;

                if (direction.sqrMagnitude > Mathf.Epsilon)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            transform.position = Vector3.MoveTowards(transform.position, _destination, speed * deltaTime);

            if (Vector3.Distance(transform.position, _destination) <= hitDistance)
            {
                _target.TakeDamage(_damage, _isCritical);
                ReleaseSelf();
            }
        }

        /// <summary>
        /// 타겟이 비행 중 죽은 뒤의 상태 — 더 이상 목적지를 향해 MoveTowards로 수렴하지 않고(그러면
        /// 원래 목적지에서 멈춰버린다), Launch 시점에 고정해둔 방향으로 화면 밖까지 계속 직진한다.
        /// </summary>
        private void TickFlyingPastDeadTarget(float deltaTime)
        {
            transform.position += _direction * speed * deltaTime;

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
