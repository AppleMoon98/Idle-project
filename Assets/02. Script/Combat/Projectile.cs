using Character;
using Core;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 타겟을 향해 유도 비행하다가 도달하면 데미지를 적용하고 스스로 풀로 반납되는 발사체.
    /// </summary>
    public sealed class Projectile : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float speed = 10f;

        [SerializeField]
        private float hitDistance = 0.2f;

        private Health _target;
        private float _damage;

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        /// <summary>
        /// 발사체를 발사한다. 풀에서 꺼낸 직후 호출되어야 한다.
        /// </summary>
        public void Launch(Health target, float damage)
        {
            _target = target;
            _damage = damage;
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
                _target.TakeDamage(_damage);
                ReleaseSelf();
            }
        }

        private void ReleaseSelf()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(gameObject);
            }
        }
    }
}
