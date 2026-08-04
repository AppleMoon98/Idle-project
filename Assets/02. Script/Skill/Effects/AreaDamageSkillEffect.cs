using Character;
using Core;
using Managers;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 시전자 주변 반경 안의 적 전체에게 magnitude만큼 데미지를 준다. 반경(SkillSO.AreaRadius)과
    /// 이펙트 프리팹(SkillSO.VfxPrefab)은 장착된 스킬 데이터에서 읽는다 - 슬롯은 어느 레이어를
    /// 때릴지(targetLayerMask)만 갖고 있다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class AreaDamageSkillEffect : MonoBehaviour, ISkillEffect
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
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, definition.AreaRadius, targetLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Health health) && !health.IsDead)
                {
                    health.TakeDamage(magnitude);
                }
            }

            PlayVfx(definition, origin.position);
        }

        // 프리팹은 radius=1 기준으로 제작하고, 실제 데미지 반경에 맞춰 스케일만 조정한다.
        private void PlayVfx(SkillSO definition, Vector3 position)
        {
            if (_pool == null || definition.VfxPrefab == null)
            {
                return;
            }

            _pool.EnsurePool(definition.VfxPrefab, vfxPoolCapacity, vfxPoolMaxSize);
            GameObject instance = _pool.Get(definition.VfxPrefab, position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * definition.AreaRadius;
            instance.GetComponent<SkillEffectVfx>().Play();
        }
    }
}
