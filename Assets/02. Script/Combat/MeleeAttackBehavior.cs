using Character;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 즉시 데미지를 적용하고 무기 스윙 모션을 재생하는 근접 공격 전략.
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class MeleeAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [SerializeField]
        private WeaponSwing weaponSwing;

        public void Execute(Transform origin, Health target, float attackPower)
        {
            target.TakeDamage(attackPower);

            if (weaponSwing != null)
            {
                weaponSwing.Play();
            }
        }
    }
}
