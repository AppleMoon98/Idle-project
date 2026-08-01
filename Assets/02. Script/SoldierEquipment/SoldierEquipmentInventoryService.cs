using System;
using System.Collections.Generic;
using Core;
using SoldierEquipment.Events;

namespace SoldierEquipment
{
    /// <summary>
    /// 병사 전용 장비를 라인(OwnedSoldierEquipment) 단위로 보관하는 서비스. Inventory.InventoryService와
    /// 동일한 스택형 구조지만, 아직 이 장비를 얻을 경로(드롭/보상 등)가 없어 AddSoldierEquipment를
    /// 수동 지급 API로 먼저 만들어둔다(EnhancementStoneService.AddStones와 같은 성격).
    /// </summary>
    public sealed class SoldierEquipmentInventoryService : IManager, IService
    {
        /// <summary>
        /// 보유 장비 한 라인을 세이브 데이터로 직렬화하기 위한 형태. SoldierEquipmentSO 참조 대신
        /// SoldierEquipmentCatalogSO 상의 인덱스로 "어떤 장비인지"를 기록한다.
        /// </summary>
        [Serializable]
        public struct OwnedSoldierEquipmentSnapshot
        {
            public int CatalogIndex;
            public int Count;
        }

        private readonly EventBus _events;
        private readonly Dictionary<SoldierEquipmentSO, OwnedSoldierEquipment> _owned = new();

        /// <summary>
        /// 현재 보유 중인 장비 라인 목록 (읽기 전용).
        /// </summary>
        public IReadOnlyCollection<OwnedSoldierEquipment> Items => _owned.Values;

        public SoldierEquipmentInventoryService(EventBus events)
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
        /// definition을 현재 보유 중인 라인을 반환한다. 보유하고 있지 않으면 false.
        /// </summary>
        public bool TryGet(SoldierEquipmentSO definition, out OwnedSoldierEquipment owned)
        {
            return _owned.TryGetValue(definition, out owned);
        }

        /// <summary>
        /// definition을 amount개 지급한다. 이미 보유 중이면 수량만 늘리고, 처음이면 새 라인을 만든다.
        /// </summary>
        public void AddSoldierEquipment(SoldierEquipmentSO definition, int amount)
        {
            if (_owned.TryGetValue(definition, out OwnedSoldierEquipment owned))
            {
                owned.Count += amount;
            }
            else
            {
                owned = new OwnedSoldierEquipment(definition, amount);
                _owned[definition] = owned;
            }

            _events.Publish(new SoldierEquipmentInventoryChangedEvent(owned, _owned.Count));
        }

        /// <summary>
        /// definition을 amount개 소모한다. 보유량이 부족하면 아무 변화 없이 false.
        /// 소모 후 0개가 되면 라인 자체를 제거한다.
        /// </summary>
        public bool TryConsume(SoldierEquipmentSO definition, int amount)
        {
            if (!_owned.TryGetValue(definition, out OwnedSoldierEquipment owned) || owned.Count < amount)
            {
                return false;
            }

            owned.Count -= amount;

            if (owned.Count <= 0)
            {
                _owned.Remove(definition);
            }

            _events.Publish(new SoldierEquipmentInventoryChangedEvent(owned, _owned.Count));
            return true;
        }

        /// <summary>
        /// 현재 보유 장비 전체를 세이브용 스냅샷으로 내보낸다. catalog에 없는(콘텐츠 삭제된) 항목은 건너뛴다.
        /// </summary>
        public OwnedSoldierEquipmentSnapshot[] ExportSnapshot(SoldierEquipmentCatalogSO catalog)
        {
            var snapshot = new List<OwnedSoldierEquipmentSnapshot>();

            foreach (OwnedSoldierEquipment owned in _owned.Values)
            {
                int catalogIndex = catalog.IndexOf(owned.Definition);

                if (catalogIndex < 0)
                {
                    continue;
                }

                snapshot.Add(new OwnedSoldierEquipmentSnapshot
                {
                    CatalogIndex = catalogIndex,
                    Count = owned.Count
                });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// 세이브 스냅샷으로 보유 장비를 복원한다. 게임플레이 획득이 아니므로
        /// SoldierEquipmentInventoryChangedEvent는 발행하지 않는다.
        /// </summary>
        public void RestoreSnapshot(OwnedSoldierEquipmentSnapshot[] snapshot, SoldierEquipmentCatalogSO catalog)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (OwnedSoldierEquipmentSnapshot entry in snapshot)
            {
                SoldierEquipmentSO definition = catalog.GetAt(entry.CatalogIndex);

                if (definition == null)
                {
                    continue;
                }

                _owned[definition] = new OwnedSoldierEquipment(definition, entry.Count);
            }
        }
    }
}
