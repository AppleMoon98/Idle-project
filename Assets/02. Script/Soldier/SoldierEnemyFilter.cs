using Character;
using Combat;
using Core;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// EnemyTracker의 대상 선택에 개입해, 다른 Soldier가 이미 추적 중인 적보다
    /// 추적되지 않은 적을 우선하도록 만든다. 대체 대상이 없으면 EnemyTracker가
    /// 알아서 최근접 대상(다른 Soldier와 겹치더라도)으로 폴백한다.
    /// </summary>
    public sealed class SoldierEnemyFilter : MonoBehaviour, ITargetFilter
    {
        private SoldierTargetRegistry _registry;

        private void Awake()
        {
            GameBootstrapper.Services?.TryGet(out _registry);
        }

        bool ITargetFilter.IsPreferred(Health candidate)
        {
            return _registry == null || !_registry.IsClaimedByOther(candidate, gameObject);
        }

        void ITargetFilter.OnTargetAcquired(Health target)
        {
            _registry?.SetClaim(gameObject, target);
        }
    }
}
