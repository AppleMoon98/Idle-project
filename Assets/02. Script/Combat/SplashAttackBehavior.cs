using Character;
using Core;
using Managers;
using UnityEngine;
using UnityEngine.Serialization;

namespace Combat
{
    /// <summary>
    /// 공성병(Siege) 전용 공격 전략 - 정타 대상에게 강력한 데미지를 그대로 적용한 뒤, 그 주변
    /// splashRadius 안의 다른 대상들에게도 (정타 데미지 × splashDamageMultiplier)만큼 스플래시
    /// 데미지를 추가로 입힌다. 정타를 맞은 대상 자신은 스플래시 스캔에서 제외해 같은 공격에
    /// 두 번 맞지 않게 한다. "속도는 매우 느리지만 스플래시 피해와 강력한 데미지가 특징"이라는
    /// 요청을 그대로 구현한 것 - 느린 이동/공격 속도는 MonsterStats_Siege의 moveSpeed/
    /// attackInterval 값만으로 표현되며, 이 컴포넌트는 공격이 실제로 발생했을 때 무엇을 하는지만
    /// 담당한다(Attacker의 공격 주기/사거리 판정은 그대로 재사용).
    ///
    /// explosionPrefab이 지정돼 있으면(sparse opt-in - 이 컴포넌트를 공유하는 Combat.BearCharge의
    /// 근접 응전에는 설정하지 않는다) 정타가 적중하는 순간 Combat.ExplosionEffect를 splashRadius
    /// 크기로 재생한다 - "공격 범위 크기로 데미지가 들어갈 때"라는 요청대로, 폭발 시각 크기가 실제
    /// 판정 반경을 그대로 나타낸다.
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class SplashAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [FormerlySerializedAs("weaponSwing")]
        [SerializeField]
        private WeaponMotion weaponMotion;

        [SerializeField]
        private float splashRadius = 2.5f;

        [SerializeField]
        private float splashDamageMultiplier = 0.5f;

        [SerializeField]
        private LayerMask splashLayerMask;

        [SerializeField]
        private GameObject explosionPrefab;

        [SerializeField]
        private int explosionPoolCapacity = 2;

        [SerializeField]
        private int explosionPoolMaxSize = 4;

        private PoolManager _pool;

        private void OnEnable()
        {
            if (explosionPrefab == null)
            {
                return;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out _pool))
            {
                _pool.EnsurePool(explosionPrefab, explosionPoolCapacity, explosionPoolMaxSize);
            }
        }

        public void Execute(Transform origin, Health target, float damage, bool isCritical)
        {
            target.TakeDamage(damage, isCritical);

            float splashDamage = damage * splashDamageMultiplier;

            if (splashDamage > 0f)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(target.transform.position, splashRadius, splashLayerMask);

                foreach (Collider2D hit in hits)
                {
                    if (hit.TryGetComponent(out Health hitHealth) && hitHealth != target && !hitHealth.IsDead)
                    {
                        hitHealth.TakeDamage(splashDamage, isCritical);
                    }
                }
            }

            SpawnExplosion(target.transform.position);

            if (weaponMotion != null)
            {
                weaponMotion.Play();
            }
        }

        private void SpawnExplosion(Vector3 position)
        {
            if (explosionPrefab == null || _pool == null)
            {
                return;
            }

            GameObject instance = _pool.Get(explosionPrefab, position, Quaternion.identity);

            if (instance.TryGetComponent(out ExplosionEffect explosion))
            {
                explosion.Play(position, splashRadius);
            }
        }
    }
}
