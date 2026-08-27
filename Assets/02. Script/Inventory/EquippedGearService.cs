using System;
using System.Collections.Generic;
using Core;
using Equipment;
using Inventory.Events;

namespace Inventory
{
    /// <summary>
    /// 슬롯별로 지금 어떤 보유 장비 라인이 장착되어 있는지 기록한다. 장착은 보유 개수를
    /// 소모하지 않는다 — 스택 중 하나를 "지금 착용 중"으로 가리키는 참조일 뿐이다. 장착 중인
    /// 라인이 합성 등으로 재료 소모되어 보유 개수가 0이 되어도 장착은 그대로 유지된다 -
    /// InventoryService가 더 이상 라인을 완전히 제거하지 않으므로(한 번 획득한 장비는 개수 0이
    /// 되어도 계속 장착 가능해야 한다는 정책), 참조가 끊길 일이 없어 예전처럼 자동 해제할
    /// 필요가 없다.
    /// </summary>
    public sealed class EquippedGearService : IManager, IService
    {
        /// <summary>
        /// 슬롯 하나의 장착 상태를 세이브 데이터로 직렬화하기 위한 형태. InventoryService.OwnedEquipmentSnapshot과
        /// 같은 이유로 EquipmentSO 참조 대신 StableId를 사용한다(GitHub 이슈 #19).
        /// </summary>
        [Serializable]
        public struct EquippedSnapshotEntry
        {
            public EquipmentType Slot;
            public string StableId;
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
                string stableId = pair.Value.Definition.StableId;

                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                snapshot.Add(new EquippedSnapshotEntry { Slot = pair.Key, StableId = stableId });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// EquippedGearService.RestoreSnapshot의 폐기 건수를 구조화된 결과로 돌려준다
        /// (Inventory.InventoryService.RestoreResult와 동일한 형태, GitHub 이슈 #46/#47).
        /// </summary>
        public readonly struct RestoreResult
        {
            public readonly int RestoredCount;
            public readonly int DiscardedMissingCatalogEntry;
            public readonly int DiscardedNotInInventory;
            public readonly int DiscardedUndefinedSlot;
            public readonly int DiscardedSlotTypeMismatch;
            public readonly int DiscardedDuplicateEquipment;

            public RestoreResult(int restoredCount, int discardedMissingCatalogEntry, int discardedNotInInventory, int discardedUndefinedSlot, int discardedSlotTypeMismatch, int discardedDuplicateEquipment)
            {
                RestoredCount = restoredCount;
                DiscardedMissingCatalogEntry = discardedMissingCatalogEntry;
                DiscardedNotInInventory = discardedNotInInventory;
                DiscardedUndefinedSlot = discardedUndefinedSlot;
                DiscardedSlotTypeMismatch = discardedSlotTypeMismatch;
                DiscardedDuplicateEquipment = discardedDuplicateEquipment;
            }

            public int TotalDiscarded => DiscardedMissingCatalogEntry + DiscardedNotInInventory + DiscardedUndefinedSlot + DiscardedSlotTypeMismatch + DiscardedDuplicateEquipment;

            public bool HasDiscardedEntries => TotalDiscarded > 0;
        }

        /// <summary>
        /// 세이브 스냅샷으로 장착 상태를 복원한다. inventory가 해당 라인을 보유하고 있어야 복원된다
        /// (InventoryService.RestoreSnapshot을 먼저 호출해야 함). 게임플레이 장착이 아니므로
        /// EquipmentEquippedEvent는 발행하지 않는다.
        ///
        /// GitHub 이슈 #46 - 매 호출마다 기존 _equipped를 먼저 비운 뒤 스냅샷 내용으로 다시
        /// 채운다(snapshot이 null이어도 마찬가지). 예전엔 비우지 않아 두 가지 방식으로 이전
        /// 데이터가 남았다: ① 새 스냅샷에 아예 없는 슬롯은 이전 장착이 그대로 유지됨 ② 새
        /// 스냅샷의 슬롯 항목이 유효성 검사(카탈로그 없음/인벤토리 미보유)에 걸려 건너뛰어져도
        /// 그 슬롯은 이전 장착을 그대로 유지함 - 특히 ②는 InventoryService.RestoreSnapshot을
        /// 먼저 정상적으로 비웠어도, 그 슬롯이 가리키던 OwnedEquipment가 이제 _owned에 존재하지
        /// 않는데 _equipped만 여전히 그 참조를 들고 있는 댕글링 상태로 이어졌다.
        ///
        /// GitHub 이슈 #47 - Equip()(정상 게임플레이 경로)은 항상 owned.Definition.EquipmentType을
        /// 키로 쓰므로 "슬롯==정의 타입"과 "정의되지 않은 enum 없음"이 저절로 성립하지만, 이
        /// 메서드는 저장된 EquippedSnapshotEntry.Slot을 검증 없이 그대로 키로 썼다 - 그래서
        /// (EquipmentType)999 같은 정의되지 않은 값이나, 같은 무기를 Weapon/Gloves 두 슬롯에
        /// 동시에 배정한 손상된 스냅샷이 그대로 런타임 상태가 됐고, ExportSnapshot은 검증 없이
        /// _equipped를 그대로 재직렬화하므로 정상 저장으로도 자동 치유되지 않았다. 이제 슬롯마다
        /// ① entry.Slot이 정의된 EquipmentType인지 ② definition.EquipmentType과 entry.Slot이
        /// 일치하는지 ③ 같은 owned 라인이 이번 복원에서 이미 다른 슬롯에 배정되지 않았는지(중복은
        /// 배열 순서상 먼저 나온 항목이 우선 - SkillLoadoutService.RestoreSnapshot 등 이 프로젝트의
        /// 다른 RestoreSnapshot이 이미 쓰는 "첫 항목 우선" 관례와 동일)를 순서대로 확인해, 하나라도
        /// 걸리면 그 항목만 완전히 버리고 나머지 유효 항목은 계속 복원한다.
        /// </summary>
        public RestoreResult RestoreSnapshot(EquippedSnapshotEntry[] snapshot, EquipmentCatalogSO catalog, InventoryService inventory)
        {
            _equipped.Clear();

            if (snapshot == null)
            {
                return new RestoreResult(0, 0, 0, 0, 0, 0);
            }

            int restoredCount = 0;
            int discardedMissingCatalog = 0;
            int discardedNotInInventory = 0;
            int discardedUndefinedSlot = 0;
            int discardedSlotTypeMismatch = 0;
            int discardedDuplicateEquipment = 0;
            var assignedThisPass = new HashSet<EquipmentSO>();

            foreach (EquippedSnapshotEntry entry in snapshot)
            {
                if (!Enum.IsDefined(typeof(EquipmentType), entry.Slot))
                {
                    discardedUndefinedSlot++;
                    continue;
                }

                EquipmentSO definition = catalog.FindByStableId(entry.StableId);

                if (definition == null)
                {
                    discardedMissingCatalog++;
                    continue;
                }

                if (definition.EquipmentType != entry.Slot)
                {
                    discardedSlotTypeMismatch++;
                    continue;
                }

                if (!inventory.TryGet(definition, out OwnedEquipment owned))
                {
                    discardedNotInInventory++;
                    continue;
                }

                if (!assignedThisPass.Add(definition))
                {
                    discardedDuplicateEquipment++;
                    continue;
                }

                _equipped[entry.Slot] = owned;
                restoredCount++;
            }

            return new RestoreResult(restoredCount, discardedMissingCatalog, discardedNotInInventory, discardedUndefinedSlot, discardedSlotTypeMismatch, discardedDuplicateEquipment);
        }
    }
}
