namespace Character
{
    /// <summary>
    /// CharacterStatsSO의 값을 복사해 들고 있는 런타임 스탯.
    /// 원본 SO 에셋을 훼손하지 않고 버프/장비 등으로 값을 변경할 수 있게 한다.
    /// </summary>
    public sealed class RuntimeStats
    {
        /// <summary>
        /// 최대 체력.
        /// </summary>
        public float MaxHealth { get; set; }

        /// <summary>
        /// 공격력.
        /// </summary>
        public float AttackPower { get; set; }

        /// <summary>
        /// 공격 사거리.
        /// </summary>
        public float AttackRange { get; set; }

        /// <summary>
        /// 이동 속도.
        /// </summary>
        public float MoveSpeed { get; set; }

        /// <summary>
        /// 공격 주기(초).
        /// </summary>
        public float AttackInterval { get; set; }

        /// <summary>
        /// CharacterStatsSO의 값을 복사해 RuntimeStats를 생성한다.
        /// </summary>
        public RuntimeStats(CharacterStatsSO source)
        {
            MaxHealth = source.MaxHealth;
            AttackPower = source.AttackPower;
            AttackRange = source.AttackRange;
            MoveSpeed = source.MoveSpeed;
            AttackInterval = source.AttackInterval;
        }
    }
}
