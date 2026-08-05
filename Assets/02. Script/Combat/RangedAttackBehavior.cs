using Character;
using Core;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 발사체를 스폰해 타겟으로 유도하는 원거리 공격 전략. 데미지는 발사체가 명중했을 때 적용된다.
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class RangedAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [SerializeField]
        private GameObject projectilePrefab;

        /// <summary>
        /// 발사체가 스폰될 위치(예: 활 오브젝트). 지정하지 않으면 origin(캐릭터 루트) 위치에서 발사한다.
        /// </summary>
        [SerializeField]
        private Transform muzzle;

        [SerializeField]
        private int poolCapacity = 4;

        [SerializeField]
        private int poolMaxSize = 16;

        private PoolManager _pool;

        private void OnEnable()
        {
            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
                _pool.EnsurePool(projectilePrefab, poolCapacity, poolMaxSize);
            }
        }

        public void Execute(Transform origin, Health target, float damage, bool isCritical)
        {
            if (_pool == null)
            {
                return;
            }

            Vector3 spawnPosition = muzzle != null ? muzzle.position : origin.position;
            GameObject instance = _pool.Get(projectilePrefab, spawnPosition, Quaternion.identity);
            instance.GetComponent<Projectile>().Launch(target, damage, isCritical);
        }
    }
}
