using Core;
using Enhancement;
using Rank.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 현재 랭크(RankSO.PlayerStatBonusPercent)에 따라 플레이어 자신의 공격력/체력에 "기본 스탯
    /// 대비 %" 보너스를 적용한다. EquipmentPossessionStatReceiver와 동일한 반영 방식
    /// (PossessionStatApplier, 직전에 적용해둔 값과의 차이만큼만 반영)을 그대로 재사용하되,
    /// 소스는 장비가 아니라 RankChangedEvent다. 이 컴포넌트는 Player 오브젝트에만 부착되므로
    /// 병사(부대) 스탯에는 전혀 영향을 주지 않는다 - Soldier 쪽은 완전히 별개인
    /// SoldierEnhancementService/SoldierStatReceiver 트랙을 그대로 쓴다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class RankStatReceiver : MonoBehaviour
    {
        private CharacterStatsProvider _statsProvider;
        private float _appliedPercent;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            float targetPercent = evt.NewRank != null ? evt.NewRank.PlayerStatBonusPercent : 0f;
            float delta = targetPercent - _appliedPercent;
            _appliedPercent = targetPercent;

            PossessionStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, EnhancementStatType.AttackPower, delta);
            PossessionStatApplier.Apply(_statsProvider.Stats, _statsProvider.BaseStats, EnhancementStatType.MaxHealth, delta);
        }
    }
}
