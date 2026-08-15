using UnityEngine;

namespace Character
{
    /// <summary>
    /// Player/Monster가 공유하는 기본 스탯 데이터 에셋.
    /// 런타임에는 이 값을 직접 수정하지 않고 RuntimeStats로 복사해서 사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStats", menuName = "Idle Project/Character/Character Stats")]
    public sealed class CharacterStatsSO : ScriptableObject
    {
        [SerializeField]
        private float maxHealth;

        [SerializeField]
        private float attackPower;

        [SerializeField]
        private float attackRange;

        [SerializeField]
        private float moveSpeed;

        [SerializeField]
        private float attackInterval;

        [SerializeField]
        private float criticalChance;

        [SerializeField]
        private float criticalDamageMultiplier = 0.5f;

        [SerializeField]
        private float damageReduction;

        [SerializeField]
        private float damageReductionPercent;

        /// <summary>
        /// 최대 체력.
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// 공격력.
        /// </summary>
        public float AttackPower => attackPower;

        /// <summary>
        /// 공격 사거리.
        /// </summary>
        public float AttackRange => attackRange;

        /// <summary>
        /// 이동 속도.
        /// </summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>
        /// 공격 주기(초).
        /// </summary>
        public float AttackInterval => attackInterval;

        /// <summary>
        /// 치명타 확률(0~1). 기본값 0(치명타 없음).
        /// </summary>
        public float CriticalChance => criticalChance;

        /// <summary>
        /// 치명타 시 추가되는 피해 보너스 비율. 기본값 0.5(치명타 시 150% 피해).
        /// </summary>
        public float CriticalDamageMultiplier => criticalDamageMultiplier;

        /// <summary>
        /// 매 피격마다 고정으로 깎이는 피해량(정률 감소가 아니라 정액 감소 — 기사(Knight)가
        /// "방어력 수치 자체보다는 받는 피해가 일정량 줄어드는" 컨셉을 원해서 이렇게 설계했다).
        /// 기본값 0(감소 없음)이라 다른 유닛에는 영향이 없다.
        /// </summary>
        public float DamageReduction => damageReduction;

        /// <summary>
        /// 받는 피해를 비율로 줄이는 정률 감소(0~1, 예: 0.15 = 15%). DamageReduction(정액)과는
        /// 별개의 메커니즘 — 둘 다 0이 아니면 정률을 먼저 적용한 뒤 정액을 뺀다(Health.TakeDamage
        /// 참고). 기본값 0(감소 없음)이라 다른 유닛에는 영향이 없다.
        /// </summary>
        public float DamageReductionPercent => damageReductionPercent;
    }
}
