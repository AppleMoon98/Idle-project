using System.Collections.Generic;
using Character;
using Character.Events;
using Core;
using Managers;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 전투 중 사망한 병사는 더 이상 자동으로 리스폰되지 않는다 — 그 슬롯은 이번 스테이지 시도가
    /// 끝날 때까지 빈 채로 남는다. 대신 스테이지가 시작될 때마다(진행/반복/사망 후퇴 전부,
    /// SoldierSpawner.OnStageChanged가 호출) ResetForNewStage가 살아있는 병사는 체력을 최대치로
    /// 되돌리고, 이번 시도 중 죽어 빈 채로 남아있던 슬롯은 그 시점의 배정을 다시 조회해 재배치한다.
    /// 슬롯별 현재 점유 인스턴스는 여기서 계속 추적해, SoldierSpawner가 배치 변경 시 "지금 그
    /// 슬롯에 누가 있는지"를 항상 정확히 물어볼 수 있게 한다.
    /// </summary>
    public sealed class SoldierRespawner
    {
        private readonly EventBus _events;
        private readonly PoolManager _pool;
        private readonly SoldierDeploymentService _deployment;
        private readonly CharacterStatsProvider _playerStats;
        private readonly Dictionary<GameObject, SoldierSpawnSlot> _activeSoldiers = new();
        private readonly Dictionary<int, GameObject> _activeBySlot = new();

        public SoldierRespawner(EventBus events, PoolManager pool, SoldierDeploymentService deployment, CharacterStatsProvider playerStats)
        {
            _events = events;
            _pool = pool;
            _deployment = deployment;
            _playerStats = playerStats;

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
        /// 현재 추적 중인(살아있는) 병사 인스턴스 전부를 SetActive만 토글한다.
        /// Stage.StageProgressTracker.SetActiveAll과 동일한 패턴 — 사망/루팅/재소환 파이프라인은
        /// 전혀 건드리지 않고 순수하게 보이기/틱을 잠깐 멈췄다 되돌리는 용도다(예: 병사 동행이
        /// 금지된 던전 오버레이 진입/종료). 비활성화된 채로도 _activeSoldiers/_activeBySlot 추적은
        /// 그대로 유지되므로, 병사가 이 상태에서 죽는 일은 없다(비활성 GameObject는 애초에
        /// CharacterDiedEvent를 발행할 수 없다).
        /// </summary>
        public void SetActiveAll(bool active)
        {
            foreach (GameObject soldier in _activeSoldiers.Keys)
            {
                if (soldier != null)
                {
                    soldier.SetActive(active);
                }
            }
        }

        /// <summary>
        /// 현재 추적 중인(살아있는) 병사 전부를 각자의 슬롯 스폰 지점으로 순간이동시킨다.
        /// SetActiveAll과 같은 성격의 유틸리티로, 사망/루팅/재소환 파이프라인은 건드리지 않고
        /// 위치/회전만 되돌린다(예: N-40 진입 시 전투 시작 위치를 예측 가능하게 맞추기 위함).
        /// </summary>
        public void ResetPositions()
        {
            foreach (KeyValuePair<GameObject, SoldierSpawnSlot> pair in _activeSoldiers)
            {
                if (pair.Key == null || pair.Value.SpawnPoint == null)
                {
                    continue;
                }

                pair.Key.transform.SetPositionAndRotation(pair.Value.SpawnPoint.position, pair.Value.SpawnPoint.rotation);
            }
        }

        /// <summary>
        /// 스테이지가 바뀔 때(진행/반복/사망 후퇴 전부) SoldierSpawner.OnStageChanged가 호출한다.
        /// 살아있는 병사는 Character.PlayerReviveOnStageChanged가 Player에게 하는 것과 동일하게
        /// 체력을 최대치로 되돌리고, slots 중 지금 살아있는 점유자가 없는 슬롯은(이번 시도 중
        /// 사망해 빈 채로 남아있던 슬롯) 그 시점의 배정을 다시 조회해 재배치한다 — 리스폰
        /// 타이머가 없어진 대신, "스테이지 시작"이 모든 슬롯을 되돌리는 유일한 시점이 된다.
        /// </summary>
        public void ResetForNewStage(IEnumerable<SoldierSpawnSlot> slots)
        {
            foreach (GameObject soldier in _activeSoldiers.Keys)
            {
                if (soldier != null && soldier.TryGetComponent(out Health health))
                {
                    health.Revive();
                }
            }

            foreach (SoldierSpawnSlot slot in slots)
            {
                if (_activeBySlot.ContainsKey(slot.SlotIndex))
                {
                    continue;
                }

                Respawn(slot);
            }
        }

        /// <summary>
        /// slotIndex를 즉시 비운다 — 지금 그 슬롯을 차지하고 있는 살아있는 인스턴스가 있으면
        /// 사망 처리 없이(루팅 등 부수효과 없이) 풀로 반환한다. 배치 재편성으로 슬롯의 배정이
        /// 바뀌거나 해제됐을 때, SoldierSpawner가 새로 스폰하기 전에 호출해 이전 점유자를 정리한다.
        /// </summary>
        public void ReleaseSlot(int slotIndex)
        {
            if (_activeBySlot.TryGetValue(slotIndex, out GameObject instance))
            {
                _activeBySlot.Remove(slotIndex);
                _activeSoldiers.Remove(instance);
                _pool.Release(instance);
            }
        }

        /// <summary>
        /// 죽은 병사를 추적에서 제거하기만 한다 — 더 이상 리스폰을 예약하지 않는다. 그 슬롯은
        /// 다음 ResetForNewStage(다음 스테이지 시작)까지 빈 채로 남는다.
        /// </summary>
        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!_activeSoldiers.TryGetValue(evt.Character, out SoldierSpawnSlot slot))
            {
                return;
            }

            _activeSoldiers.Remove(evt.Character);
            _activeBySlot.Remove(slot.SlotIndex);
        }

        /// <summary>
        /// 그 시점의 배정을 다시 조회해 재배치한다. 배정이 없거나(로스터 미배치/해제) 프리팹을
        /// 가리키지 못하면 조용히 재배치하지 않는다.
        /// </summary>
        private void Respawn(SoldierSpawnSlot slot)
        {
            if (!SoldierSpawnUtility.TrySpawnAssigned(_pool, _deployment, slot, _playerStats, out GameObject instance))
            {
                return;
            }

            RegisterSpawned(instance, slot);
        }
    }
}
