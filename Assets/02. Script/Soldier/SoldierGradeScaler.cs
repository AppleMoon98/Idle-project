using Character;
using Core;
using Equipment;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 이 병사의 MaxHealth/AttackPower를 "플레이어 현재 스탯 × SoldierSO.Grade의 메인 등급
    /// 지분율"만큼 보정한다. 플레이어 스탯은 강화/장비 등으로 계속 변하므로 스폰 시 한 번만
    /// 적용하지 않고 recomputeInterval마다 다시 계산한다 — Character.EquipmentStatReceiver와
    /// 동일한 "직전에 적용해둔 값과의 차이만큼만 반영" 방식이라, SoldierStatReceiver(병사 강화
    /// 보너스)가 Stats에 이미 반영해둔 값 위에 이 지분만 얹고, 다음 재계산 때는 그 차이만
    /// 조정한다(둘이 서로의 몫을 지우지 않음). 주기적 재계산에서는 Health.Revive를 부르지 않는다
    /// (부르면 매번 만피가 되어 사실상 무적이 됨) — Character.EquipmentStatReceiver가 장비 교체로
    /// MaxHealth가 바뀔 때도 Revive를 부르지 않는 것과 같은 이유. 스폰 직후 최초 1회(Initialize)만
    /// Revive를 불러 갓 스폰된 병사가 최종 등급 반영 최대 체력으로 시작하게 한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class SoldierGradeScaler : MonoBehaviour, ITickable
    {
        [SerializeField]
        private SoldierGradeConfigSO config;

        [SerializeField]
        private float recomputeInterval = 1f;

        private CharacterStatsProvider _statsProvider;
        private Health _health;
        private CharacterStatsProvider _playerStats;
        private EquipmentGradeSO _grade;
        private float _appliedHealthBonus;
        private float _appliedAttackBonus;
        private float _elapsed;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// playerStats/grade를 주입하고 즉시 한 번 계산한 뒤 체력을 최종 최대치로 채운다.
        /// 스폰(초기 배치/리스폰) 직후 SoldierSpawnUtility가 호출한다 — 매번 새로 호출되므로
        /// 직전에 추적하던 보너스도 여기서 0으로 리셋해 리스폰 시 누적되지 않는다.
        /// </summary>
        public void Initialize(CharacterStatsProvider playerStats, EquipmentGradeSO grade)
        {
            _playerStats = playerStats;
            _grade = grade;
            _appliedHealthBonus = 0f;
            _appliedAttackBonus = 0f;

            Recompute();
            _health.Revive();
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < recomputeInterval)
            {
                return;
            }

            _elapsed = 0f;
            Recompute();
        }

        private void Recompute()
        {
            if (config == null || _grade == null || _playerStats == null)
            {
                return;
            }

            float percent = config.GetPercent(_grade);
            float targetHealthBonus = _playerStats.Stats.MaxHealth * percent;
            float targetAttackBonus = _playerStats.Stats.AttackPower * percent;

            _statsProvider.Stats.MaxHealth += targetHealthBonus - _appliedHealthBonus;
            _statsProvider.Stats.AttackPower += targetAttackBonus - _appliedAttackBonus;

            _appliedHealthBonus = targetHealthBonus;
            _appliedAttackBonus = targetAttackBonus;
        }
    }
}
