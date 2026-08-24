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
    /// 가리키게만 한다(유닛은 배치 여부와 무관하게 항상 보유 상태를 유지). 슬롯 자체는 랭크와
    /// 무관하게 항상 전부(TotalSlotCount) 열려 있다 — 실제 배치 상한은 오직 코스트 예산
    /// (RankSO.MaxDeploymentCost)뿐이다. 원래는 RankSO.MaxDeployableSquads로 슬롯 수도 함께
    /// 랭크 게이팅했으나, 병과 최소 코스트가 1이라 어느 랭크에서도 "코스트 예산 ≤ 언락 슬롯 수"가
    /// 항상 성립해 슬롯 게이팅이 실질적으로 아무것도 막지 못하는 죽은 제약이었다 — 제거했다.
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
        /// 부대 편성 팝업 상단 배치 그리드의 열 수. 실제 씬의 TacticSlotGrid(GridLayoutGroup)는
        /// FixedRowCount=4로 설정돼 있어 20칸이 5열×4행으로 채워진다("4열×5행"이라는 옛 기록은
        /// 오기 — 씬 설정이 항상 기준이다).
        /// </summary>
        public const int GridColumns = 5;

        /// <summary>
        /// 부대 편성 팝업 상단 배치 그리드의 행 수.
        /// </summary>
        public const int GridRows = 4;

        /// <summary>
        /// UI가 flat 슬롯 인덱스를 "부대" 단위(부대당 GridColumns×GridRows칸 = 부대 편성 팝업 상단
        /// 배치 그리드, 총 6부대)로 묶어 보여줄 때 쓰는 공용 상수 — 부대 N(0-based)은 슬롯
        /// [N*SlotsPerSquad, (N+1)*SlotsPerSquad) 구간이다. 배정 로직(TryAssign 등)은 이 구분을
        /// 전혀 모른다 — 여전히 flat 인덱스로만 동작한다. 원래 12(단순 세로 목록 12줄)였다가, 그
        /// 목록이 실제 배치 대상인 그리드로 바뀌면서(section DI) 그리드 칸 수에 맞춰 20으로 늘었다.
        /// </summary>
        public const int SlotsPerSquad = GridColumns * GridRows;

        public const int SquadCount = 6;

        /// <summary>
        /// 랭크와 무관하게 항상 열려 있는 전체 슬롯 수(SquadCount × SlotsPerSquad) — TryAssign/
        /// TryDeploy가 유효한 slotIndex 범위를 판단하는 유일한 기준이다.
        /// </summary>
        public const int TotalSlotCount = SquadCount * SlotsPerSquad;

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
        /// 현재 랭크에서 배치 가능한 전체 코스트 예산(Rank.RankSO.MaxDeploymentCost) — 랭크가
        /// 오를수록 늘어난다(예: 시골 소년 10, 병사 20, 십인 대장 30). 랭크 정보가 없으면 0.
        /// </summary>
        public int GetMaxDeploymentCost()
        {
            return _rankService?.CurrentRank != null ? _rankService.CurrentRank.MaxDeploymentCost : 0;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _slotToInstanceId.Clear();
        }

        /// <summary>
        /// instanceId 유닛을 slotIndex에 그대로 배정한다(코스트 예산 확인 없이 기계적으로 슬롯만
        /// 채운다) — 로스터에 없는 유닛이거나, slotIndex가 유효 범위(TotalSlotCount) 밖이면 아무
        /// 변화 없이 false. 그 유닛이 이미 다른 슬롯에 배치돼 있었다면 그 슬롯에서는 자동으로
        /// 해제한다 — 한 병사는 동시에 한 슬롯만 차지할 수 있다. 코스트 예산 확인은 이 메서드를
        /// 호출하는 TryDeploy가 담당한다(단일 책임 분리).
        /// </summary>
        public bool TryAssign(int slotIndex, int instanceId)
        {
            if (slotIndex < 0 || slotIndex >= TotalSlotCount)
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
        /// 현재 배치된 모든 유닛의 Soldier.SoldierSO.Cost 합. UI(부대 편성 팝업의
        /// "usedCost/MaxDeploymentCost" 표시)와 TryDeploy의 예산 확인이 함께 쓴다.
        /// </summary>
        public int GetTotalDeployedCost()
        {
            int total = 0;

            foreach (int instanceId in _slotToInstanceId.Values)
            {
                if (_roster.TryGet(instanceId, out OwnedSoldier owned) && owned.Definition != null)
                {
                    total += owned.Definition.Cost;
                }
            }

            return total;
        }

        /// <summary>
        /// instanceId 유닛을 코스트 예산(GetMaxDeploymentCost) 안에서 자동으로 빈 슬롯을 찾아
        /// 배치한다("부대 편성" 팝업 하단의 보유 병사 카드를 탭했을 때 쓰는 진입점 — 더 이상
        /// 플레이어가 슬롯/부대를 직접 고르지 않는다). 이미 배치돼 있으면 AlreadyDeployed, 로스터에
        /// 없으면 NotInRoster, 열린 슬롯이 하나도 없으면 NoFreeSlot, 코스트 예산을 넘기면
        /// CostExceeded를 reason에 담아 false를 반환한다.
        /// </summary>
        public bool TryDeploy(int instanceId, out DeploymentFailureReason reason)
        {
            if (TryGetSlotOf(instanceId, out _))
            {
                reason = DeploymentFailureReason.AlreadyDeployed;
                return false;
            }

            if (!_roster.TryGet(instanceId, out OwnedSoldier owned) || owned.Definition == null)
            {
                reason = DeploymentFailureReason.NotInRoster;
                return false;
            }

            int freeSlotIndex = -1;

            for (int i = 0; i < TotalSlotCount; i++)
            {
                if (!_slotToInstanceId.ContainsKey(i))
                {
                    freeSlotIndex = i;
                    break;
                }
            }

            if (freeSlotIndex < 0)
            {
                reason = DeploymentFailureReason.NoFreeSlot;
                return false;
            }

            if (GetTotalDeployedCost() + owned.Definition.Cost > GetMaxDeploymentCost())
            {
                reason = DeploymentFailureReason.CostExceeded;
                return false;
            }

            reason = DeploymentFailureReason.None;
            return TryAssign(freeSlotIndex, instanceId);
        }

        /// <summary>
        /// instanceId 유닛의 배치를 해제한다("부대 편성" 팝업 상단의 배치된 병사 카드를 탭했을
        /// 때 쓰는 진입점). 배치돼 있지 않으면 false.
        /// </summary>
        public bool TryUndeploy(int instanceId)
        {
            if (!TryGetSlotOf(instanceId, out int slotIndex))
            {
                return false;
            }

            Unassign(slotIndex);
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
        /// slotIndex(전역)의 "방어자" 슬롯 - 같은 부대 안에서 한 행 위(그리드상 더 앞줄, 1열=row0에
        /// 가까운 쪽)의 같은 열 슬롯을 가리킨다. 방패벽 전술에서 "이 슬롯 유닛을 지키는 방패병이
        /// 있다면 몇 번 슬롯인가"를 구하는 데 쓴다(SquadShieldWallCoordinator 참고) - 1열(row0)이
        /// 방패병이라는 관례와 일치시켰다(예전엔 반대 방향(row+1)이었는데, "1열=방패병"이 실제로는
        /// 아무도 보호하지 않는 반대 결과가 나와서 방향을 뒤집었다). slotIndex가 이미 부대 내 첫
        /// 행(1열, 가장 앞줄)이면 그보다 더 앞이 없으므로 null.
        /// </summary>
        public static int? GetProtectorSlotIndex(int slotIndex)
        {
            int relative = slotIndex % SlotsPerSquad;
            int row = relative / GridColumns;

            if (row <= 0)
            {
                return null;
            }

            return slotIndex - GridColumns;
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
