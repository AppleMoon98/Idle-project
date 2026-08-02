using Character;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 시전자 주변 반경 안의 적 전체에게 magnitude만큼 데미지를 준다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class AreaDamageSkillEffect : MonoBehaviour, ISkillEffect
    {
        [SerializeField]
        private float radius = 3f;

        [SerializeField]
        private LayerMask targetLayerMask;

        public void Execute(Transform origin, float magnitude)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, radius, targetLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Health health) && !health.IsDead)
                {
                    health.TakeDamage(magnitude);
                }
            }
        }
    }
}
