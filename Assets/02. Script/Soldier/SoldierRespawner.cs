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
    /// 대기 중 재편성(다른 유닛으로 교체/해제)이 있었다면 항상 최신 배정을 반영한다. 슬롯별 현재
    /// 점유 인스턴스도 여기서 함께 관리해, SoldierSpawner가 배치 변경 시 "지금 그 슬롯에 누가
    /// 있는지"를 항상 정확히 물어볼 수 있게 한다(사망→대기→자연 리스폰 사이에도 어긋나지 않도록).
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
        private readonly Dictionary<int, GameObject> _activeBySlot = new();
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
            _activeBySlot[slot.SlotIndex] = soldier;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 소유자(SoldierSpawner) 파괴 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// slotIndex를 즉시 비운다 — 대기 중인 리스폰 타이머를 취소하고, 지금 그 슬롯을 차지하고
        /// 있는 살아있는 인스턴스가 있으면 사망 처리 없이(루팅 등 부수효과 없이) 풀로 반환한다.
        /// 배치 재편성으로 슬롯의 배정이 바뀌거나 해제됐을 때, SoldierSpawner가 새로 스폰하기
        /// 전에 호출해 이전 점유자를 정리한다.
        /// </summary>
        public void ReleaseSlot(int slotIndex)
        {
            _pendingRespawns.RemoveAll(pending => pending.Slot.SlotIndex == slotIndex);

            if (_activeBySlot.TryGetValue(slotIndex, out GameObject instance))
            {
                _activeBySlot.Remove(slotIndex);
                _activeSoldiers.Remove(instance);
                _pool.Release(instance);
            }
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
            _activeBySlot.Remove(slot.SlotIndex);
            _pendingRespawns.Add(new PendingRespawn(slot, _respawnDelay));
        }

        /// <summary>
        /// 그 시점의 배정을 다시 조회해 재소환한다. 그사이 배정이 해제/변경됐으면(배정된 유닛이
        /// 없거나 프리팹을 가리키지 못하면) 조용히 재소환하지 않는다.
        /// </summary>
        private void Respawn(SoldierSpawnSlot slot)
        {
            if (!SoldierSpawnUtility.TrySpawnAssigned(_pool, _deployment, slot, out GameObject instance))
            {
                return;
            }

            RegisterSpawned(instance, slot);
        }
    }
}
