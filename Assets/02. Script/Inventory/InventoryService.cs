using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Equipment;
using Inventory.Events;
using Loot.Events;

namespace Inventory
{
    /// <summary>
    /// 드롭된 장비를 라인(OwnedEquipment) 단위로 보관하는 서비스. ItemDroppedEvent를 구독해
    /// 이미 보유 중인 장비면 스택 카운트를 올리고, 처음 보는 장비면 새 라인을 추가한다.
    /// 변경 시 InventoryChangedEvent를 발행해 UI 등이 구독할 수 있게 한다.
    /// </summary>
    public sealed class InventoryService : IManager, IService
    {
        /// <summary>
        /// 보유 장비 한 라인을 세이브 데이터로 직렬화하기 위한 형태. EquipmentSO 참조 대신
        /// EquipmentCatalogSO 상의 인덱스로 "어떤 장비인지"를 기록한다(PlayerPrefs는 에셋 참조를 담을 수 없음).
        /// </summary>
        [Serializable]
        public struct OwnedEquipmentSnapshot
        {
            public int CatalogIndex;
            public int Count;
            public int EnhancementLevel;
        }

        private readonly EventBus _events;
        private readonly Dictionary<EquipmentSO, OwnedEquipment> _owned = new();

        /// <summary>
        /// 현재 보유 중인 장비 라인 목록 (읽기 전용).
        /// </summary>
        public IReadOnlyCollection<OwnedEquipment> Items => _owned.Values;

        public InventoryService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events.Subscribe<ItemDroppedEvent>(OnItemDropped);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<ItemDroppedEvent>(OnItemDropped);
        }

        /// <summary>
        /// definition을 현재 보유 중인 라인을 반환한다. 보유하고 있지 않으면 false.
        /// </summary>
        public bool TryGet(EquipmentSO definition, out OwnedEquipment owned)
        {
            return _owned.TryGetValue(definition, out owned);
        }

        /// <summary>
        /// definition을 amount개 소모한다(합성/강화 재료). 보유량이 부족하면 아무 변화 없이 false.
        /// 0개가 되어도 라인 자체는 제거하지 않고 그대로 둔다 - 한 번이라도 획득한 장비는 개수가
        /// 0이 되어도 목록에 남아 장착 가능해야 한다는 정책(EquipmentSlotPopupUI/EquippedGearService
        /// 참고) 때문에, "보유한 적이 있다"는 사실 자체를 잃지 않는다.
        /// </summary>
        public bool TryConsume(EquipmentSO definition, int amount)
        {
            if (!_owned.TryGetValue(definition, out OwnedEquipment owned) || owned.Count < amount)
            {
                return false;
            }

            owned.Count -= amount;

            _events.Publish(new InventoryChangedEvent(owned, _owned.Count));
            return true;
        }

        /// <summary>
        /// definition 라인의 강화 레벨을 levels만큼 올린다. 보유하고 있지 않으면 아무 일도 하지 않는다.
        /// </summary>
        public void AddEnhancementLevel(EquipmentSO definition, int levels)
        {
            if (!_owned.TryGetValue(definition, out OwnedEquipment owned))
            {
                return;
            }

            owned.EnhancementLevel += levels;
            _events.Publish(new InventoryChangedEvent(owned, _owned.Count));
        }

        /// <summary>
        /// slot에 속한 보유 라인을 등급 낮은 것부터 정렬해 반환한다. "슬롯 전체 일괄 처리" 기능
        /// (EquipmentEnhancementService.TryEnhanceAll, EquipmentFusionService.TryFuseAll)이 공유하는 조회 로직.
        /// </summary>
        public List<EquipmentSO> GetLinesBySlotSortedByGrade(EquipmentType slot, EquipmentGradeCatalogSO gradeCatalog)
        {
            return _owned.Values
                .Where(owned => owned.Definition.EquipmentType == slot)
                .OrderBy(owned => gradeCatalog.IndexOf(owned.Definition.Grade))
                .Select(owned => owned.Definition)
                .ToList();
        }

        /// <summary>
        /// 현재 보유 장비 전체를 세이브용 스냅샷으로 내보낸다. catalog에 없는(콘텐츠 삭제된) 항목은 건너뛴다.
        /// </summary>
        public OwnedEquipmentSnapshot[] ExportSnapshot(EquipmentCatalogSO catalog)
        {
            var snapshot = new List<OwnedEquipmentSnapshot>();

            foreach (OwnedEquipment owned in _owned.Values)
            {
                int catalogIndex = catalog.IndexOf(owned.Definition);

                if (catalogIndex < 0)
                {
                    continue;
                }

                snapshot.Add(new OwnedEquipmentSnapshot
                {
                    CatalogIndex = catalogIndex,
                    Count = owned.Count,
                    EnhancementLevel = owned.EnhancementLevel
                });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// 세이브 스냅샷으로 보유 장비를 복원한다. 게임플레이 획득이 아니므로 InventoryChangedEvent는 발행하지 않는다.
        /// </summary>
        public void RestoreSnapshot(OwnedEquipmentSnapshot[] snapshot, EquipmentCatalogSO catalog)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (OwnedEquipmentSnapshot entry in snapshot)
            {
                EquipmentSO definition = catalog.GetAt(entry.CatalogIndex);

                if (definition == null)
                {
                    continue;
                }

                _owned[definition] = new OwnedEquipment(definition, entry.Count, entry.EnhancementLevel);
            }
        }

        private void OnItemDropped(ItemDroppedEvent evt)
        {
            if (_owned.TryGetValue(evt.Equipment, out OwnedEquipment owned))
            {
                owned.Count++;
            }
            else
            {
                owned = new OwnedEquipment(evt.Equipment, count: 1, enhancementLevel: 0);
                _owned[evt.Equipment] = owned;
            }

            _events.Publish(new InventoryChangedEvent(owned, _owned.Count));
        }
    }
}
