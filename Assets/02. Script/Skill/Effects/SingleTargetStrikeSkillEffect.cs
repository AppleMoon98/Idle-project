using Character;
using Core;
using Managers;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 탐지 범위 안 최근접 적 1체에게 magnitude만큼 강타를 날린다. 탐지 범위(SkillSO.StrikeRange)와
    /// 이펙트 프리팹(SkillSO.VfxPrefab)은 장착된 스킬 데이터에서 읽는다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class SingleTargetStrikeSkillEffect : MonoBehaviour, ISkillEffect
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private PoolManager _pool;

        private void OnEnable()
        {
            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
            }
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(origin.position, definition.StrikeRange, targetLayerMask);

            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - origin.position).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            }

            if (nearest == null)
            {
                return;
            }

            Vector3 hitPosition = nearest.transform.position;
            nearest.TakeDamage(magnitude);
            PlayVfx(definition, hitPosition);
        }

        private void PlayVfx(SkillSO definition, Vector3 position)
        {
            if (_pool == null || definition.VfxPrefab == null)
            {
                return;
            }

            _pool.EnsurePool(definition.VfxPrefab, vfxPoolCapacity, vfxPoolMaxSize);
            GameObject instance = _pool.Get(definition.VfxPrefab, position, Quaternion.identity);
            instance.GetComponent<SkillEffectVfx>().Play();
        }
    }
}
