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

        public void Execute(Transform origin, Health target, float attackPower)
        {
            if (_pool == null)
            {
                return;
            }

            GameObject instance = _pool.Get(projectilePrefab, origin.position, Quaternion.identity);
            instance.GetComponent<Projectile>().Launch(target, attackPower);
        }
    }
}
