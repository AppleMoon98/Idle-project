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
    }
}
