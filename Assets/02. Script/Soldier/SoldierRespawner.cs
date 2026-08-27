using System.Collections.Generic;
using Character;
using Character.Events;
using Core;
using Managers;
using Services;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 전투 중 사망한 병사는 더 이상 자동으로 리스폰되지 않는다 — 그 슬롯은 이번 스테이지 시도가
    /// 끝날 때까지 빈 채로 남는다. 대신 스테이지가 시작될 때마다(진행/반복/사망 후퇴 전부,
    /// SoldierSpawner.OnStageChanged가 호출) ResetForNewStage가 살아있는 병사는 체력을 최대치로
    /// 되돌리고, 이번 시도 중 죽어 빈 채로 남아있던 슬롯은 재배치한다. 스폰 좌표는 항상
    /// Soldier.SoldierGridPlacement로 "현재 배정된 슬롯 전체"를 기준으로 다시 계산한다 — 슬롯
    /// 하나만 봐서는 사거리 정렬 순서를 알 수 없으므로, 호출마다 전체 슬롯 배열을 받아 한 번에
    /// 계산한다. 슬롯별 현재 점유 인스턴스는 여기서 계속 추적해, SoldierSpawner가 배치 변경 시
    /// "지금 그 슬롯에 누가 있는지"를 항상 정확히 물어볼 수 있게 한다.
    /// </summary>
    public sealed class SoldierRespawner
    {
        private readonly EventBus _events;
        private readonly PoolManager _pool;
        private readonly SoldierDeploymentService _deployment;
        private readonly CharacterStatsProvider _playerStats;
        private readonly CameraFollowService _cameraFollowService;
        private readonly Dictionary<GameObject, SoldierSpawnSlot> _activeSoldiers = new();
        private readonly Dictionary<int, GameObject> _activeBySlot = new();

        public SoldierRespawner(EventBus events, PoolManager pool, SoldierDeploymentService deployment, CharacterStatsProvider playerStats, CameraFollowService cameraFollowService)
        {
            _events = events;
            _pool = pool;
            _deployment = deployment;
            _playerStats = playerStats;
            _cameraFollowService = cameraFollowService;

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
        /// 재활성화(active=true) 시에는 OnEnable이 Soldier.SoldierStatReceiver를 통해
        /// RuntimeStats.MoveSpeed를 본연 속도로 초기화해버리므로, 이 경로가 끝난 뒤
        /// Soldier.SquadMovementSyncService.Resync로 부대 클램프를 명시적으로 다시 적용한다 —
        /// 안 하면 던전을 한 번 다녀온 병사가 이후 계속 자기 본연 속도로 각자 따로 걷게 된다.
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

            if (active && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SquadMovementSyncService squadSync))
            {
                foreach (GameObject soldier in _activeSoldiers.Keys)
                {
                    squadSync.Resync(soldier);
                }
            }
        }

        /// <summary>
        /// slots(현재 배정된 슬롯 전체) 기준으로 그리드 좌표를 다시 계산해, 지금 추적 중인(살아
        /// 있는) 병사 전부를 그 자리로 순간이동시킨다. SetActiveAll과 같은 성격의 유틸리티로,
        /// 사망/루팅/재소환 파이프라인은 건드리지 않고 위치/회전만 되돌린다(예: N-40 진입 시
        /// 전투 시작 위치를 예측 가능하게 맞추기 위함).
        /// </summary>
        public void ResetPositions(SoldierSpawnSlot[] slots)
        {
            Dictionary<int, Vector3> placements = ComputePlacements(slots);

            foreach (KeyValuePair<GameObject, SoldierSpawnSlot> pair in _activeSoldiers)
            {
                if (pair.Key == null || !placements.TryGetValue(pair.Value.SlotIndex, out Vector3 position))
                {
                    continue;
                }

                Transform anchor = pair.Value.ResolvePositionAnchor(position);
                pair.Key.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
        }

        /// <summary>
        /// 스테이지가 바뀔 때(진행/반복/사망 후퇴 전부) SoldierSpawner.OnStageChanged가 호출한다.
        /// 살아있는 병사는 Character.PlayerReviveOnStageChanged가 Player에게 하는 것과 동일하게
        /// 체력을 최대치로 되돌리고, slots 중 지금 살아있는 점유자가 없는 슬롯은(이번 시도 중
        /// 사망해 빈 채로 남아있던 슬롯) 재배치한다 — 리스폰 타이머가 없어진 대신, "스테이지 시작"이
        /// 모든 슬롯을 되돌리는 유일한 시점이 된다.
        /// </summary>
        public void ResetForNewStage(SoldierSpawnSlot[] slots)
        {
            foreach (GameObject soldier in _activeSoldiers.Keys)
            {
                if (soldier != null && soldier.TryGetComponent(out Health health))
                {
                    health.Revive();
                }
            }

            Dictionary<int, Vector3> placements = ComputePlacements(slots);

            foreach (SoldierSpawnSlot slot in slots)
            {
                if (_activeBySlot.ContainsKey(slot.SlotIndex))
                {
                    continue;
                }

                if (placements.TryGetValue(slot.SlotIndex, out Vector3 position))
                {
                    TrySpawnSlot(slot, position);
                }
            }
        }

        /// <summary>
        /// slotIndex를 즉시 비운다 — 지금 그 슬롯을 차지하고 있는 살아있는 인스턴스가 있으면
        /// 사망 처리 없이(루팅 등 부수효과 없이) 풀로 반환한다. 배치 재편성으로 슬롯의 배정이
        /// 바뀌거나 해제됐을 때, SoldierSpawner가 새로 스폰하기 전에 호출해 이전 점유자를 정리한다.
        /// CharacterDiedEvent를 발행하지 않는 경로라(GitHub 이슈 #40), Soldier.SquadMovementSyncService.
        /// Unregister를 여기서 직접 호출해줘야 한다 — 안 그러면 그 인스턴스가 비활성화된 채 풀에
        /// 있으면서도 이전 부대의 이동속도/교전 집계에 영원히 유령으로 남는다(배치 해제처럼 그 뒤로
        /// 다시 Register가 호출되지 않는 경로에서는 어떤 방법으로도 저절로 정리되지 않았다).
        /// </summary>
        public void ReleaseSlot(int slotIndex)
        {
            if (_activeBySlot.TryGetValue(slotIndex, out GameObject instance))
            {
                _activeBySlot.Remove(slotIndex);
                _activeSoldiers.Remove(instance);

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SquadMovementSyncService squadSync))
                {
                    squadSync.Unregister(instance);
                }

                _pool.Release(instance);
            }
        }

        /// <summary>
        /// slots(현재 배정된 슬롯 전체) 기준으로 SoldierGridPlacement 그리드 좌표를 계산한다.
        /// 슬롯 하나만 봐서는 사거리 정렬 순서를 알 수 없으므로, 스폰/리셋 호출마다 전체 배열을
        /// 한 번에 계산해 SlotIndex → 좌표 맵으로 돌려준다.
        /// </summary>
        public Dictionary<int, Vector3> ComputePlacements(SoldierSpawnSlot[] slots)
        {
            Vector3 boundsCenter = _cameraFollowService != null ? _cameraFollowService.HomeLocalPosition : Vector3.zero;
            Vector2 boundsHalfExtent = _cameraFollowService != null ? _cameraFollowService.GetWorldBoundsHalfExtent() : Vector2.zero;
            return SoldierGridPlacement.ComputePlacements(slots, _deployment, boundsCenter, boundsHalfExtent);
        }

        /// <summary>
        /// 이미 계산된 position에 slot의 현재 배정을 스폰한다. 배정이 없거나(로스터 미배치/해제)
        /// 프리팹을 가리키지 못하면 조용히 스폰하지 않는다.
        /// </summary>
        public bool TrySpawnSlot(SoldierSpawnSlot slot, Vector3 position)
        {
            if (!SoldierSpawnUtility.TrySpawnAssigned(_pool, _deployment, slot, position, _playerStats, out GameObject instance))
            {
                return false;
            }

            RegisterSpawned(instance, slot);
            return true;
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
    }
}
