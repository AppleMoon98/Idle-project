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
    /// 현재 랭크(RankSO.MaxDeployableSquads × SlotsPerSquad)를 그대로 따른다 — SoldierSpawner가 이미
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

        /// <summary>
        /// UI가 flat 슬롯 인덱스를 "부대" 단위(부대당 20명 = 부대 편성 팝업 상단 4x5 배치 그리드,
        /// 총 6부대)로 묶어 보여줄 때 쓰는 공용 상수 — 부대 N(0-based)은 슬롯 [N*SlotsPerSquad,
        /// (N+1)*SlotsPerSquad) 구간이다. 배정 로직(TryAssign 등)은 이 구분을 전혀 모른다 — 여전히
        /// flat 인덱스로만 동작한다. 원래 12(단순 세로 목록 12줄)였다가, 그 목록이 실제 배치
        /// 대상인 4x5 그리드로 바뀌면서(section DI) 그리드 칸 수에 맞춰 20으로 늘었다.
        /// </summary>
        public const int SlotsPerSquad = 20;

        public const int SquadCount = 6;

        /// <summary>
        /// 한 부대(SlotsPerSquad개 슬롯)에 동시에 배치할 수 있는 실제 인원 상한. 슬롯 칸 수(20,
        /// 부대 편성 팝업의 4x5 그리드)와는 별개 개념 — 그리드 칸은 전부 보이지만 실제로 채울 수
        /// 있는 인원은 이 값까지만이다.
        /// </summary>
        public const int MaxDeployedPerSquad = 12;

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
        /// 현재 랭크에서 동시에 배치할 수 있는 슬롯 수 — RankSO.MaxDeployableSquads(완전히 꾸릴 수
        /// 있는 부대 수) × SlotsPerSquad로 환산한다. 랭크 정보가 없으면(설정 누락) 0.
        /// </summary>
        public int GetMaxUnlockedSlotCount()
        {
            int squads = _rankService?.CurrentRank != null ? _rankService.CurrentRank.MaxDeployableSquads : 0;
            return squads * SlotsPerSquad;
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

            bool hasExistingSlot = TryGetSlotOf(instanceId, out int existingSlot);

            // slotIndex가 이미 비어있는 자리를 새로 채우는 경우에만 인원 상한을 확인한다 - 이미
            // 채워진 슬롯을 덮어쓰거나(부대원 교체) 같은 부대 안에서 옮기는 경우는 부대 전체 인원이
            // 늘어나지 않으므로 막을 이유가 없다.
            if (!_slotToInstanceId.ContainsKey(slotIndex))
            {
                int squadIndex = slotIndex / SlotsPerSquad;
                bool vacatesWithinSameSquad = hasExistingSlot && existingSlot != slotIndex && existingSlot / SlotsPerSquad == squadIndex;
                int projectedCount = GetOccupiedCount(squadIndex) + 1 - (vacatesWithinSameSquad ? 1 : 0);

                if (projectedCount > MaxDeployedPerSquad)
                {
                    return false;
                }
            }

            if (hasExistingSlot && existingSlot != slotIndex)
            {
                _slotToInstanceId.Remove(existingSlot);
                _events.Publish(new SoldierDeploymentChangedEvent(existingSlot));
            }

            _slotToInstanceId[slotIndex] = instanceId;
            _events.Publish(new SoldierDeploymentChangedEvent(slotIndex));
            return true;
        }

        /// <summary>
        /// squadIndex(SlotsPerSquad개 슬롯 구간)에 현재 배정이 있는 슬롯 수.
        /// </summary>
        private int GetOccupiedCount(int squadIndex)
        {
            int start = squadIndex * SlotsPerSquad;
            int end = start + SlotsPerSquad;
            int count = 0;

            foreach (int occupiedSlot in _slotToInstanceId.Keys)
            {
                if (occupiedSlot >= start && occupiedSlot < end)
                {
                    count++;
                }
            }

            return count;
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
        /// slotA와 slotB의 배정을 서로 맞바꾼다. 한쪽만 채워져 있으면 그 유닛이 반대쪽으로 옮겨가고
        /// 원래 자리는 빈 칸이 되는 것(이동)과 결과적으로 동일하다 — "스왑"과 "이동"을 굳이 나누지
        /// 않고 한 메서드로 처리한다. 두 슬롯 다 비어있으면 아무 일도 일어나지 않는다(이벤트도
        /// 발행하지 않는다). TryAssign과 달리 잠금 해제 여부(GetMaxUnlockedSlotCount)를 확인하지
        /// 않는다 — 이미 배정이 존재하는 슬롯끼리(또는 그런 슬롯과 인접한 빈 슬롯) 주고받는
        /// 것이라, 애초에 슬롯이 잠겨 있었다면 그 자리에 배정이 있을 수 없다.
        /// </summary>
        public void Swap(int slotA, int slotB)
        {
            bool hasA = _slotToInstanceId.TryGetValue(slotA, out int instanceAtA);
            bool hasB = _slotToInstanceId.TryGetValue(slotB, out int instanceAtB);

            if (!hasA && !hasB)
            {
                return;
            }

            if (hasA)
            {
                _slotToInstanceId[slotB] = instanceAtA;
            }
            else
            {
                _slotToInstanceId.Remove(slotB);
            }

            if (hasB)
            {
                _slotToInstanceId[slotA] = instanceAtB;
            }
            else
            {
                _slotToInstanceId.Remove(slotA);
            }

            _events.Publish(new SoldierDeploymentChangedEvent(slotA));
            _events.Publish(new SoldierDeploymentChangedEvent(slotB));
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
