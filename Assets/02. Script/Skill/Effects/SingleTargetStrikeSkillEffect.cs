using Character;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 탐지 범위 안 최근접 적 1체에게 magnitude만큼 강타를 날린다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class SingleTargetStrikeSkillEffect : MonoBehaviour, ISkillEffect
    {
        [SerializeField]
        private float range = 8f;

        [SerializeField]
        private LayerMask targetLayerMask;

        public void Execute(Transform origin, float magnitude)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(origin.position, range, targetLayerMask);

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

            nearest?.TakeDamage(magnitude);
        }
    }
}
