using Character;
using UnityEngine;
using UnityEngine.Serialization;

namespace Combat
{
    /// <summary>
    /// 즉시 데미지를 적용하고 무기 모션(휘두르기/찌르기 등)을 재생하는 근접 공격 전략.
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [FormerlySerializedAs("weaponSwing")]
        [SerializeField]
        private WeaponMotion weaponMotion;

        public void Execute(Transform origin, Health target, float damage, bool isCritical)
        {
            target.TakeDamage(damage, isCritical);

            if (weaponMotion != null)
            {
                weaponMotion.Play();
            }
        }
    }
}
