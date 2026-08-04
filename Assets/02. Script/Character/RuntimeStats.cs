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
        /// 치명타 확률(0~1).
        /// </summary>
        public float CriticalChance { get; set; }

        /// <summary>
        /// 치명타 시 추가되는 피해 보너스 비율. 최종 피해 = AttackPower * (1 + CriticalDamageMultiplier).
        /// </summary>
        public float CriticalDamageMultiplier { get; set; }

        /// <summary>
        /// CharacterStatsSO의 값을 복사해 RuntimeStats를 생성한다.
        /// </summary>
        public RuntimeStats(CharacterStatsSO source)
        {
            ResetTo(source);
        }

        /// <summary>
        /// 모든 필드를 source(원본 SO) 값으로 되돌린다. 풀링되어 재사용되는 캐릭터(병사 등)가
        /// 스폰될 때마다 이전 생애에서 누적된 버프/강화 적용치를 지우고 원본 기준으로 다시
        /// 시작하기 위한 것이다 — StageMonsterScaler.ApplyScale이 매 스폰마다 baseStats 기준으로
        /// 다시 계산하는 것과 같은 이유.
        /// </summary>
        public void ResetTo(CharacterStatsSO source)
        {
            MaxHealth = source.MaxHealth;
            AttackPower = source.AttackPower;
            AttackRange = source.AttackRange;
            MoveSpeed = source.MoveSpeed;
            AttackInterval = source.AttackInterval;
            CriticalChance = source.CriticalChance;
            CriticalDamageMultiplier = source.CriticalDamageMultiplier;
        }
    }
}
