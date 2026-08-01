using System.Collections.Generic;
using Character.Events;
using Core;
using Managers;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 스폰된 병사가 사망하면 일정 시간(respawnDelay) 뒤 그 슬롯의 배정을 다시 확인해 재소환한다.
    /// 사망 시점의 배정을 기억해두지 않고 리스폰 시점에 SoldierDeploymentService를 다시 조회하므로,
    /// 대기 중 재편성(다른 유닛으로 교체/해제)이 있었다면 항상 최신 배정을 반영한다.
    /// </summary>
    public sealed class SoldierRespawner : ITickable
    {
        /// <summary>
        /// 사망 후 재소환까지 대기 중인 하나의 항목.
        /// </summary>
        private sealed class PendingRespawn
        {
            public readonly SoldierSpawnSlot Slot;
            public float Remaining;

            public PendingRespawn(SoldierSpawnSlot slot, float remaining)
            {
                Slot = slot;
                Remaining = remaining;
            }
        }

        private readonly EventBus _events;
        private readonly PoolManager _pool;
        private readonly SoldierDeploymentService _deployment;
        private readonly float _respawnDelay;
        private readonly Dictionary<GameObject, SoldierSpawnSlot> _activeSoldiers = new();
        private readonly List<PendingRespawn> _pendingRespawns = new();

        public SoldierRespawner(EventBus events, PoolManager pool, SoldierDeploymentService deployment, float respawnDelay)
        {
            _events = events;
            _pool = pool;
            _deployment = deployment;
            _respawnDelay = respawnDelay;

            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 새로 스폰한 병사 인스턴스를, 사망 시 참조할 슬롯과 함께 등록한다.
        /// </summary>
        public void RegisterSpawned(GameObject soldier, SoldierSpawnSlot slot)
        {
            _activeSoldiers[soldier] = slot;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 소유자(SoldierSpawner) 파괴 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        void ITickable.Tick(float deltaTime)
        {
            for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
            {
                PendingRespawn pending = _pendingRespawns[i];
                pending.Remaining -= deltaTime;

                if (pending.Remaining > 0f)
                {
                    continue;
                }

                _pendingRespawns.RemoveAt(i);
                Respawn(pending.Slot);
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!_activeSoldiers.TryGetValue(evt.Character, out SoldierSpawnSlot slot))
            {
                return;
            }

            _activeSoldiers.Remove(evt.Character);
            _pendingRespawns.Add(new PendingRespawn(slot, _respawnDelay));
        }

        /// <summary>
        /// 그 시점의 배정을 다시 조회해 재소환한다. 그사이 배정이 해제/변경됐으면(배정된 유닛이
        /// 없거나 프리팹을 가리키지 못하면) 조용히 재소환하지 않는다.
        /// </summary>
        private void Respawn(SoldierSpawnSlot slot)
        {
            if (_deployment == null || !_deployment.TryGetAssigned(slot.SlotIndex, out OwnedSoldier owned) || owned.Definition.Prefab == null)
            {
                return;
            }

            _pool.EnsurePool(owned.Definition.Prefab, 1, 1);
            GameObject instance = _pool.Get(owned.Definition.Prefab, slot.SpawnPoint.position, slot.SpawnPoint.rotation);

            if (instance.TryGetComponent(out SoldierBehaviorController controller))
            {
                controller.Initialize(owned.InstanceId, slot.SpawnPoint);
            }

            RegisterSpawned(instance, slot);
        }
    }
}
