using System;
using System.Collections.Generic;
using Core;
using Equipment;
using Inventory.Events;

namespace Inventory
{
    /// <summary>
    /// 슬롯별로 지금 어떤 보유 장비 라인이 장착되어 있는지 기록한다. 장착은 보유 개수를
    /// 소모하지 않는다 — 스택 중 하나를 "지금 착용 중"으로 가리키는 참조일 뿐이다.
    /// </summary>
    public sealed class EquippedGearService : IManager, IService
    {
        /// <summary>
        /// 슬롯 하나의 장착 상태를 세이브 데이터로 직렬화하기 위한 형태. InventoryService.OwnedEquipmentSnapshot과
        /// 같은 이유로 EquipmentSO 참조 대신 카탈로그 인덱스를 사용한다.
        /// </summary>
        [Serializable]
        public struct EquippedSnapshotEntry
        {
            public EquipmentType Slot;
            public int CatalogIndex;
        }

        private readonly EventBus _events;
        private readonly Dictionary<EquipmentType, OwnedEquipment> _equipped = new();

        public EquippedGearService(EventBus events)
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
        /// slot에 현재 장착 중인 장비 라인. 없으면 null.
        /// </summary>
        public OwnedEquipment GetEquipped(EquipmentType slot)
        {
            return _equipped.TryGetValue(slot, out OwnedEquipment owned) ? owned : null;
        }

        /// <summary>
        /// owned를 그 슬롯에 장착한다.
        /// </summary>
        public void Equip(OwnedEquipment owned)
        {
            _equipped[owned.Definition.EquipmentType] = owned;
            _events.Publish(new EquipmentEquippedEvent(owned.Definition.EquipmentType));
        }

        /// <summary>
        /// 현재 슬롯별 장착 상태를 세이브용 스냅샷으로 내보낸다. catalog에 없는 항목은 건너뛴다.
        /// </summary>
        public EquippedSnapshotEntry[] ExportSnapshot(EquipmentCatalogSO catalog)
        {
            var snapshot = new List<EquippedSnapshotEntry>();

            foreach (KeyValuePair<EquipmentType, OwnedEquipment> pair in _equipped)
            {
                int catalogIndex = catalog.IndexOf(pair.Value.Definition);

                if (catalogIndex < 0)
                {
                    continue;
                }

                snapshot.Add(new EquippedSnapshotEntry { Slot = pair.Key, CatalogIndex = catalogIndex });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// 세이브 스냅샷으로 장착 상태를 복원한다. inventory가 해당 라인을 보유하고 있어야 복원된다
        /// (InventoryService.RestoreSnapshot을 먼저 호출해야 함). 게임플레이 장착이 아니므로
        /// EquipmentEquippedEvent는 발행하지 않는다.
        /// </summary>
        public void RestoreSnapshot(EquippedSnapshotEntry[] snapshot, EquipmentCatalogSO catalog, InventoryService inventory)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (EquippedSnapshotEntry entry in snapshot)
            {
                EquipmentSO definition = catalog.GetAt(entry.CatalogIndex);

                if (definition == null || !inventory.TryGet(definition, out OwnedEquipment owned))
                {
                    continue;
                }

                _equipped[entry.Slot] = owned;
            }
        }
    }
}
