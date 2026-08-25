using System;
using System.Collections.Generic;
using Behavior;
using Core;
using Soldier.Events;

namespace Soldier
{
    /// <summary>
    /// 보유 중인 병사 개별 유닛(OwnedSoldier)을 관리하는 서비스. Gacha 도메인이 AddSoldier를
    /// 호출해 새 유닛을 추가하지만, 이 서비스 자체는 Gacha의 존재를 전혀 모른다
    /// (InventoryService가 Loot을 모르는 것과 같은 방향의 의존성).
    /// </summary>
    public sealed class SoldierRosterService : IManager, IService
    {
        /// <summary>
        /// 보유 병사 유닛 하나를 세이브 데이터로 직렬화하기 위한 형태. SoldierSO/BehaviorProfileSO 참조 대신
        /// 각각의 StableId로 "어떤 병사·프로필인지"를 기록한다(PlayerPrefs는 에셋 참조를 담을 수 없음,
        /// 배열 인덱스 대신 StableId를 쓰는 이유는 GitHub 이슈 #19). BehaviorProfileStableId가
        /// 비어있으면 프로필 미배정.
        /// </summary>
        [Serializable]
        public struct OwnedSoldierSnapshot
        {
            public string StableId;
            public int InstanceId;
            public string BehaviorProfileStableId;
        }

        private readonly EventBus _events;
        private readonly Dictionary<int, OwnedSoldier> _roster = new();
        private int _nextInstanceId;

        /// <summary>
        /// 현재 보유 중인 병사 유닛 목록 (읽기 전용).
        /// </summary>
        public IReadOnlyCollection<OwnedSoldier> Roster => _roster.Values;

        public SoldierRosterService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// definition 병사의 새 개별 유닛을 로스터에 추가하고 SoldierRosterChangedEvent를 발행한다.
        /// </summary>
        public OwnedSoldier AddSoldier(SoldierSO definition)
        {
            var owned = new OwnedSoldier(definition, _nextInstanceId);
            _nextInstanceId++;

            _roster[owned.InstanceId] = owned;
            _events.Publish(new SoldierRosterChangedEvent(owned, _roster.Count));

            return owned;
        }

        /// <summary>
        /// instanceId 유닛을 반환한다. 없으면 false.
        /// </summary>
        public bool TryGet(int instanceId, out OwnedSoldier owned)
        {
            return _roster.TryGetValue(instanceId, out owned);
        }

        /// <summary>
        /// instanceId 유닛에 행동 프로필을 배정한다(null이면 배정 해제). 유닛이 없으면 아무 일도 하지 않는다.
        /// </summary>
        public void SetBehaviorProfile(int instanceId, BehaviorProfileSO profile)
        {
            if (!_roster.TryGetValue(instanceId, out OwnedSoldier owned))
            {
                return;
            }

            owned.BehaviorProfile = profile;
            _events.Publish(new SoldierBehaviorProfileChangedEvent(instanceId));
        }

        /// <summary>
        /// 현재 로스터 전체를 세이브용 스냅샷으로 내보낸다. catalog에 없는(콘텐츠 삭제된) 항목은 건너뛴다.
        /// 다음 발급 번호(nextInstanceId)도 함께 반환해, 복원 후 재발급 시 ID가 겹치지 않게 한다.
        /// </summary>
        public OwnedSoldierSnapshot[] ExportSnapshot(SoldierCatalogSO catalog, BehaviorProfileCatalogSO behaviorProfileCatalog, out int nextInstanceId)
        {
            var snapshot = new List<OwnedSoldierSnapshot>();

            foreach (OwnedSoldier owned in _roster.Values)
            {
                string stableId = owned.Definition.StableId;

                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                snapshot.Add(new OwnedSoldierSnapshot
                {
                    StableId = stableId,
                    InstanceId = owned.InstanceId,
                    BehaviorProfileStableId = owned.BehaviorProfile != null ? owned.BehaviorProfile.StableId : null
                });
            }

            nextInstanceId = _nextInstanceId;
            return snapshot.ToArray();
        }

        /// <summary>
        /// 세이브 스냅샷으로 로스터를 복원한다. 게임플레이 획득이 아니므로 SoldierRosterChangedEvent는 발행하지 않는다.
        /// </summary>
        public void RestoreSnapshot(OwnedSoldierSnapshot[] snapshot, SoldierCatalogSO catalog, BehaviorProfileCatalogSO behaviorProfileCatalog, int nextInstanceId)
        {
            _nextInstanceId = nextInstanceId;

            if (snapshot == null)
            {
                return;
            }

            foreach (OwnedSoldierSnapshot entry in snapshot)
            {
                SoldierSO definition = catalog.FindByStableId(entry.StableId);

                if (definition == null)
                {
                    continue;
                }

                _roster[entry.InstanceId] = new OwnedSoldier(definition, entry.InstanceId)
                {
                    BehaviorProfile = behaviorProfileCatalog.FindByStableId(entry.BehaviorProfileStableId)
                };
            }
        }
    }
}
