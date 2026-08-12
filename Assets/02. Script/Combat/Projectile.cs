using Character;
using Core;
using Managers;
using Stage.Events;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 타겟을 향해 유도 비행하다가 도달하면 데미지를 적용하고 스스로 풀로 반납되는 발사체.
    /// 타겟이 죽으면(Health.IsDead) 자연히 반납되지만, 그것만으로는 부족하다 — Character.Health.Die가
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

        private Health _target;
        private float _damage;
        private bool _isCritical;
        private bool _released;

        private void OnEnable()
        {
            _released = false;
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
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
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_target == null || _target.IsDead)
            {
                ReleaseSelf();
                return;
            }

            Vector3 targetPosition = _target.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) <= hitDistance)
            {
                _target.TakeDamage(_damage, _isCritical);
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
