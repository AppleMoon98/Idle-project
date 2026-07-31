using UnityEngine;

namespace Character
{
    /// <summary>
    /// 스테이지 난이도 배율을 몬스터의 RuntimeStats에 적용한다. Monster 전용 컴포넌트.
    /// 배율 계산 정책은 Stage 도메인(StageDifficultyConfigSO)이 소유하며,
    /// 이 컴포넌트는 스폰 시점에 전달받은 배율을 원본 스탯(BaseStats) 기준으로 재계산해 적용만 한다.
    /// 풀링으로 재사용되는 오브젝트이므로 매번 원본 기준으로 다시 계산해야 배율이 누적되지 않는다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class StageMonsterScaler : MonoBehaviour
    {
        private CharacterStatsProvider _statsProvider;
        private Health _health;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        /// <summary>
        /// 원본 스탯에 multiplier를 곱해 MaxHealth/AttackPower를 재계산하고,
        /// 변경된 최대체력이 현재체력에도 반영되도록 Revive()를 다시 호출한다.
        /// </summary>
        public void ApplyScale(float multiplier)
        {
            CharacterStatsSO baseStats = _statsProvider.BaseStats;
            RuntimeStats stats = _statsProvider.Stats;

            stats.MaxHealth = baseStats.MaxHealth * multiplier;
            stats.AttackPower = baseStats.AttackPower * multiplier;

            _health.Revive();
        }
    }
}
