using System.Collections.Generic;
using Character.Events;
using Core;
using Managers;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 스폰된 병사가 사망하면 일정 시간(respawnDelay) 뒤 원래 스폰 지점에서 다시 소환한다.
    /// </summary>
    public sealed class SoldierRespawner : ITickable
    {
        /// <summary>
        /// 사망 후 재소환까지 대기 중인 하나의 항목.
        /// </summary>
        private sealed class PendingRespawn
        {
            public readonly SoldierSpawnEntry Entry;
            public float Remaining;

            public PendingRespawn(SoldierSpawnEntry entry, float remaining)
            {
                Entry = entry;
                Remaining = remaining;
            }
        }

        private readonly EventBus _events;
        private readonly PoolManager _pool;
        private readonly float _respawnDelay;
        private readonly Dictionary<GameObject, SoldierSpawnEntry> _activeSoldiers = new();
        private readonly List<PendingRespawn> _pendingRespawns = new();

        public SoldierRespawner(EventBus events, PoolManager pool, float respawnDelay)
        {
            _events = events;
            _pool = pool;
            _respawnDelay = respawnDelay;

            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 새로 스폰한 병사 인스턴스를, 사망 시 참조할 스폰 엔트리와 함께 등록한다.
        /// </summary>
        public void RegisterSpawned(GameObject soldier, SoldierSpawnEntry entry)
        {
            _activeSoldiers[soldier] = entry;
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
                Respawn(pending.Entry);
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!_activeSoldiers.TryGetValue(evt.Character, out SoldierSpawnEntry entry))
            {
                return;
            }

            _activeSoldiers.Remove(evt.Character);
            _pendingRespawns.Add(new PendingRespawn(entry, _respawnDelay));
        }

        private void Respawn(SoldierSpawnEntry entry)
        {
            GameObject instance = _pool.Get(entry.SoldierPrefab, entry.SpawnPoint.position, entry.SpawnPoint.rotation);
            RegisterSpawned(instance, entry);
        }
    }
}
