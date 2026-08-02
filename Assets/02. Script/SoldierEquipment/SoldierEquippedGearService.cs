using System;
using System.Collections.Generic;
using Core;
using SoldierEquipment.Events;

namespace SoldierEquipment
{
    /// <summary>
    /// 병사 유닛별 슬롯 장착 상태를 기록한다. Inventory.EquippedGearService와 달리 장착이 보유
    /// 개수를 실제로 소모한다 — 플레이어는 착용자가 한 명뿐이라 스택을 그냥 가리키기만 해도
    /// 됐지만, 병사는 여럿이 같은 라인을 동시에 원할 수 있어 그대로 두면 재고 1개를 모든 병사가
    /// 동시에 "장착"할 수 있게 된다(재고 없이 무한정 공유). 그래서 장착 시 1개를 소모하고,
    /// 해제하거나 다른 라인으로 교체할 때 이전 장비 1개를 재고로 돌려준다.
    /// </summary>
    public sealed class SoldierEquippedGearService : IManager, IService
    {
        /// <summary>
        /// 병사 한 유닛의 슬롯 하나 장착 상태를 세이브 데이터로 직렬화하기 위한 형태.
        /// </summary>
        [Serializable]
        public struct EquippedSnapshotEntry
        {
            public int InstanceId;
            public SoldierEquipmentType Slot;
            public int CatalogIndex;
        }

        private readonly EventBus _events;
        private readonly SoldierEquipmentInventoryService _inventory;
        private readonly Dictionary<(int InstanceId, SoldierEquipmentType Slot), OwnedSoldierEquipment> _equipped = new();

        public SoldierEquippedGearService(EventBus events, SoldierEquipmentInventoryService inventory)
        {
            _events = events;
            _inventory = inventory;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// instanceId 유닛의 slot에 현재 장착 중인 장비 라인. 없으면 null.
        /// </summary>
        public OwnedSoldierEquipment GetEquipped(int instanceId, SoldierEquipmentType slot)
        {
            return _equipped.TryGetValue((instanceId, slot), out OwnedSoldierEquipment owned) ? owned : null;
        }

        /// <summary>
        /// instanceId 유닛의 owned.Definition.SlotType 슬롯에 owned를 장착한다. 재고에서 1개를
        /// 소모하며, 재고가 없으면(다른 병사들이 이미 다 착용 중이면) 아무 변화 없이 false.
        /// 이미 같은 라인을 장착 중이면 재소모 없이 true(멱등). 그 슬롯에 다른 라인이 장착돼
        /// 있었다면 그 1개는 재고로 돌아간다.
        /// </summary>
        public bool Equip(int instanceId, OwnedSoldierEquipment owned)
        {
            SoldierEquipmentType slot = owned.Definition.SlotType;
            var key = (instanceId, slot);

            if (_equipped.TryGetValue(key, out OwnedSoldierEquipment current) && current == owned)
            {
                return true;
            }

            if (!_inventory.TryConsume(owned.Definition, 1))
            {
                return false;
            }

            if (current != null)
            {
                _inventory.AddSoldierEquipment(current.Definition, 1);
            }

            _equipped[key] = owned;
            _events.Publish(new SoldierEquipmentEquippedEvent(instanceId, slot));
            return true;
        }

        /// <summary>
        /// instanceId 유닛의 slot 장착을 해제하고, 착용 중이던 1개를 재고로 돌려준다.
        /// 장착 중이 아니면 아무 변화 없다.
        /// </summary>
        public void Unequip(int instanceId, SoldierEquipmentType slot)
        {
            var key = (instanceId, slot);

            if (!_equipped.TryGetValue(key, out OwnedSoldierEquipment current))
            {
                return;
            }

            _equipped.Remove(key);
            _inventory.AddSoldierEquipment(current.Definition, 1);
            _events.Publish(new SoldierEquipmentEquippedEvent(instanceId, slot));
        }

        /// <summary>
        /// 현재 모든 유닛의 슬롯별 장착 상태를 세이브용 스냅샷으로 내보낸다. catalog에 없는 항목은 건너뛴다.
        /// </summary>
        public EquippedSnapshotEntry[] ExportSnapshot(SoldierEquipmentCatalogSO catalog)
        {
            var snapshot = new List<EquippedSnapshotEntry>();

            foreach (KeyValuePair<(int InstanceId, SoldierEquipmentType Slot), OwnedSoldierEquipment> pair in _equipped)
            {
                int catalogIndex = catalog.IndexOf(pair.Value.Definition);

                if (catalogIndex < 0)
                {
                    continue;
                }

                snapshot.Add(new EquippedSnapshotEntry
                {
                    InstanceId = pair.Key.InstanceId,
                    Slot = pair.Key.Slot,
                    CatalogIndex = catalogIndex
                });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// 세이브 스냅샷으로 장착 상태를 복원한다. inventory가 해당 라인을 보유하고 있어야 복원된다
        /// (SoldierEquipmentInventoryService.RestoreSnapshot을 먼저 호출해야 함). 세이브된 보유 수량은
        /// 이미 장착분까지 포함해 기록된 값이므로, 복원 시에는 재고를 다시 소모하지 않는다
        /// (게임플레이 장착이 아니라 상태 복원이기 때문— SoldierEquipmentEquippedEvent도 발행하지 않는다).
        /// </summary>
        public void RestoreSnapshot(EquippedSnapshotEntry[] snapshot, SoldierEquipmentCatalogSO catalog, SoldierEquipmentInventoryService inventory)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (EquippedSnapshotEntry entry in snapshot)
            {
                SoldierEquipmentSO definition = catalog.GetAt(entry.CatalogIndex);

                if (definition == null || !inventory.TryGet(definition, out OwnedSoldierEquipment owned))
                {
                    continue;
                }

                _equipped[(entry.InstanceId, entry.Slot)] = owned;
            }
        }
    }
}
