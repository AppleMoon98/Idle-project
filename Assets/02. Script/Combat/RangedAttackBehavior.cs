using Character;
using Core;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 발사체를 스폰해 타겟 방향으로 쏘는 원거리 공격 전략. 데미지는 발사체(Combat.Projectile)가
    /// 실제로 무언가에 명중했을 때 적용된다 - 이 컴포넌트는 발사만 담당하고, 발사 이후의 명중
    /// 판정은 전적으로 발사체 자신이 targetLayerMask로 스스로 검사한다(Attacker.targetLayerMask는
    /// private이라 재사용할 수 없어 별도 필드를 둔다 - Combat.SplashAttackBehavior.splashLayerMask와
    /// 같은 이유의 같은 관례).
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class RangedAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [SerializeField]
        private GameObject projectilePrefab;

        [SerializeField]
        private LayerMask targetLayerMask;

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
            instance.GetComponent<Projectile>().Launch(target, damage, isCritical, targetLayerMask);
        }
    }
}
