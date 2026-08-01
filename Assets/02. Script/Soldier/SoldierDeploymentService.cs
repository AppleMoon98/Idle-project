using System;
using System.Collections.Generic;
using Core;
using Rank;
using Soldier.Events;

namespace Soldier
{
    /// <summary>
    /// 배치 슬롯(SoldierSpawnSlot.SlotIndex)마다 로스터의 어떤 유닛이 나가 있는지 기록한다.
    /// EquippedGearService와 같은 성격 — 배정은 로스터에서 유닛을 "빼지" 않고 슬롯이 InstanceId를
    /// 가리키게만 한다(유닛은 배치 여부와 무관하게 항상 보유 상태를 유지). 슬롯 잠금 해제 수는
    /// 현재 랭크(RankSO.MaxDeployableSoldiers)를 그대로 따른다 — SoldierSpawner가 이미
    /// RankService.IsAtLeast()를 직접 참조하는 것과 같은 방향의 의존성이다.
    /// </summary>
    public sealed class SoldierDeploymentService : IManager, IService
    {
        /// <summary>
        /// 슬롯 하나의 배정 상태를 세이브 데이터로 직렬화하기 위한 형태.
        /// </summary>
        [Serializable]
        public struct DeploymentSnapshotEntry
        {
            public int SlotIndex;
            public int InstanceId;
        }

        private readonly EventBus _events;
        private readonly SoldierRosterService _roster;
        private readonly RankService _rankService;
        private readonly Dictionary<int, int> _slotToInstanceId = new();

        public SoldierDeploymentService(EventBus events, SoldierRosterService roster, RankService rankService)
        {
            _events = events;
            _roster = roster;
            _rankService = rankService;
        }

        /// <summary>
        /// 현재 랭크에서 동시에 배치할 수 있는 슬롯 수. 랭크 정보가 없으면(설정 누락) 0.
        /// </summary>
        public int GetMaxUnlockedSlotCount()
        {
            return _rankService?.CurrentRank != null ? _rankService.CurrentRank.MaxDeployableSoldiers : 0;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _slotToInstanceId.Clear();
        }

        /// <summary>
        /// instanceId 유닛을 slotIndex에 배정한다. 로스터에 없는 유닛이거나, slotIndex가 현재
        /// 랭크로 아직 잠금 해제되지 않았으면 아무 변화 없이 false. 그 유닛이 이미 다른 슬롯에
        /// 배치돼 있었다면 그 슬롯에서는 자동으로 해제한다 — 한 병사는 동시에 한 슬롯만 차지할 수
        /// 있다(같은 병사를 여러 슬롯에 무한정 배치하는 것을 막는다).
        /// </summary>
        public bool TryAssign(int slotIndex, int instanceId)
        {
            if (slotIndex >= GetMaxUnlockedSlotCount())
            {
                return false;
            }

            if (!_roster.TryGet(instanceId, out _))
            {
                return false;
            }

            if (TryGetSlotOf(instanceId, out int existingSlot) && existingSlot != slotIndex)
            {
                _slotToInstanceId.Remove(existingSlot);
                _events.Publish(new SoldierDeploymentChangedEvent(existingSlot));
            }

            _slotToInstanceId[slotIndex] = instanceId;
            _events.Publish(new SoldierDeploymentChangedEvent(slotIndex));
            return true;
        }

        /// <summary>
        /// instanceId 유닛이 현재 배치돼 있는 슬롯을 찾는다(역방향 조회). 배치돼 있지 않으면 false.
        /// </summary>
        public bool TryGetSlotOf(int instanceId, out int slotIndex)
        {
            foreach (KeyValuePair<int, int> pair in _slotToInstanceId)
            {
                if (pair.Value == instanceId)
                {
                    slotIndex = pair.Key;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        /// <summary>
        /// slotIndex의 배정을 해제한다.
        /// </summary>
        public void Unassign(int slotIndex)
        {
            if (_slotToInstanceId.Remove(slotIndex))
            {
                _events.Publish(new SoldierDeploymentChangedEvent(slotIndex));
            }
        }

        /// <summary>
        /// slotIndex에 배정된 유닛을 반환한다. 배정이 없거나 배정된 유닛이 로스터에서 사라졌으면 false.
        /// </summary>
        public bool TryGetAssigned(int slotIndex, out OwnedSoldier owned)
        {
            owned = null;
            return _slotToInstanceId.TryGetValue(slotIndex, out int instanceId) && _roster.TryGet(instanceId, out owned);
        }

        /// <summary>
        /// 현재 배정 상태 전체를 세이브용 스냅샷으로 내보낸다.
        /// </summary>
        public DeploymentSnapshotEntry[] ExportSnapshot()
        {
            var snapshot = new List<DeploymentSnapshotEntry>();

            foreach (KeyValuePair<int, int> pair in _slotToInstanceId)
            {
                snapshot.Add(new DeploymentSnapshotEntry { SlotIndex = pair.Key, InstanceId = pair.Value });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// 세이브 스냅샷으로 배정 상태를 복원한다. 로스터 복원 이후에 호출해야 한다(TryGetAssigned가
        /// 로스터를 조회하므로, 로스터에 없는 InstanceId는 자연히 무시된다 — 별도 검증 불필요).
        /// </summary>
        public void RestoreSnapshot(DeploymentSnapshotEntry[] snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (DeploymentSnapshotEntry entry in snapshot)
            {
                _slotToInstanceId[entry.SlotIndex] = entry.InstanceId;
            }
        }
    }
}
