using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 플레이어에 부착되어, EnemyTracker가 선택한 최종 타겟을 PlayerTargetTracker에 기록한다.
    /// 타겟 선택 로직 자체에는 관여하지 않는다(항상 선호 후보로 취급해 기존 최근접 선택을 그대로 둔다).
    /// </summary>
    public sealed class PlayerTargetMarker : MonoBehaviour, ITargetFilter
    {
        private PlayerTargetTracker _tracker;

        private void Awake()
        {
            GameBootstrapper.Services?.TryGet(out _tracker);
        }

        bool ITargetFilter.IsPreferred(Health candidate)
        {
            return true;
        }

        void ITargetFilter.OnTargetAcquired(Health target)
        {
            _tracker?.SetCurrentTarget(target);
        }
    }
}
