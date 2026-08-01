using System;
using System.Collections.Generic;
using Core;
using SoldierEquipment.Events;

namespace SoldierEquipment
{
    /// <summary>
    /// 병사 유닛별 슬롯 장착 상태를 기록한다. Inventory.EquippedGearService와 같은 성격이지만,
    /// 플레이어는 한 명뿐이라 슬롯만으로 키가 됐던 것과 달리, 병사는 개별 유닛이 여럿이므로
    /// (InstanceId, Slot) 조합으로 키를 잡는다. 장착은 보유 개수를 소모하지 않는다 — 스택 중 하나를
    /// "지금 착용 중"으로 가리키는 참조일 뿐이다.
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
        private readonly Dictionary<(int InstanceId, SoldierEquipmentType Slot), OwnedSoldierEquipment> _equipped = new();

        public SoldierEquippedGearService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events.Subscribe<SoldierEquipmentInventoryChangedEvent>(OnInventoryChanged);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<SoldierEquipmentInventoryChangedEvent>(OnInventoryChanged);
        }

        /// <summary>
        /// instanceId 유닛의 slot에 현재 장착 중인 장비 라인. 없으면 null.
        /// </summary>
        public OwnedSoldierEquipment GetEquipped(int instanceId, SoldierEquipmentType slot)
        {
            return _equipped.TryGetValue((instanceId, slot), out OwnedSoldierEquipment owned) ? owned : null;
        }

        /// <summary>
        /// instanceId 유닛의 owned.Definition.SlotType 슬롯에 owned를 장착한다.
        /// </summary>
        public void Equip(int instanceId, OwnedSoldierEquipment owned)
        {
            SoldierEquipmentType slot = owned.Definition.SlotType;
            _equipped[(instanceId, slot)] = owned;
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
        /// (SoldierEquipmentInventoryService.RestoreSnapshot을 먼저 호출해야 함). 게임플레이 장착이
        /// 아니므로 SoldierEquipmentEquippedEvent는 발행하지 않는다.
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

        /// <summary>
        /// 장착 중인 라인이 재료로 전부 소모되어 보유 개수가 0이 된 경우, 그 라인을 장착 중이던
        /// 모든 유닛의 해당 슬롯 장착을 해제한다. Inventory.EquippedGearService의 동일한 gotcha 대응.
        /// </summary>
        private void OnInventoryChanged(SoldierEquipmentInventoryChangedEvent evt)
        {
            if (evt.Changed.Count > 0)
            {
                return;
            }

            var staleKeys = new List<(int InstanceId, SoldierEquipmentType Slot)>();

            foreach (KeyValuePair<(int InstanceId, SoldierEquipmentType Slot), OwnedSoldierEquipment> pair in _equipped)
            {
                if (pair.Value == evt.Changed)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            foreach ((int InstanceId, SoldierEquipmentType Slot) key in staleKeys)
            {
                _equipped.Remove(key);
                _events.Publish(new SoldierEquipmentEquippedEvent(key.InstanceId, key.Slot));
            }
        }
    }
}
