using Character;
using Core;
using Enhancement;
using SoldierEnhancement;
using SoldierEnhancement.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// SoldierEnhancementService의 병사 전용 강화를 이 병사 유닛의 RuntimeStats에 적용한다.
    /// Character.StatEnhancementReceiver(Player)와 달리, 병사는 PoolManager로 계속 스폰/디스폰을
    /// 반복하며 RuntimeStats(CharacterStatsProvider가 캐싱)가 풀 재사용 사이에도 그대로 남아있으므로,
    /// 매 스폰(OnEnable)마다 원본 기준으로 리셋한 뒤 현재 누적 레벨을 통째로 다시 적용해야 한다
    /// (StageMonsterScaler.ApplyScale과 동일한 이유). 이후 살아있는 동안의 실시간 강화는 이벤트로
    /// 받은 델타만 추가 적용한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    [RequireComponent(typeof(Health))]
    public sealed class SoldierStatReceiver : MonoBehaviour
    {
        private CharacterStatsProvider _statsProvider;
        private Health _health;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            ApplyCumulativeFromBase();
            GameBootstrapper.Events?.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
        }

        private void OnSoldierStatEnhanced(SoldierStatEnhancedEvent evt)
        {
            RuntimeStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, evt.StatType, evt.ValuePerLevel);
        }

        private void ApplyCumulativeFromBase()
        {
            _statsProvider.Stats.ResetTo(_statsProvider.BaseStats);

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierEnhancementService service))
            {
                return;
            }

            foreach (EnhancementStatType statType in service.StatTypes)
            {
                float cumulativeDelta = service.GetValuePerLevel(statType) * service.GetLevel(statType);
                RuntimeStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, statType, cumulativeDelta);
            }

            _health.Revive();
        }
    }
}
