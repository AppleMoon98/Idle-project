using Core;
using Stage.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 스테이지가 바뀔 때마다(클리어로 전진하든, 사망으로 후퇴하든) 체력을 최대치로 되돌린다.
    /// Player는 풀링되지 않아 PoolManager의 OnSpawned를 못 받으므로, StageChangedEvent라는
    /// Stage 도메인 이벤트를 구독해 반응한다 — Enhancement/StatEnhancedEvent를
    /// Character.StatEnhancementReceiver가 구독하는 것과 동일한 결합 방향.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PlayerReviveOnStageChanged : MonoBehaviour
    {
        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _health.Revive();
        }
    }
}
