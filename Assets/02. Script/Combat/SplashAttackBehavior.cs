using Character;
using UnityEngine;

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
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class SplashAttackBehavior : MonoBehaviour, IAttackBehavior
    {
        [SerializeField]
        private WeaponSwing weaponSwing;

        [SerializeField]
        private float splashRadius = 2.5f;

        [SerializeField]
        private float splashDamageMultiplier = 0.5f;

        [SerializeField]
        private LayerMask splashLayerMask;

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

            if (weaponSwing != null)
            {
                weaponSwing.Play();
            }
        }
    }
}
