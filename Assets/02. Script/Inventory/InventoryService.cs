using System.Collections.Generic;
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
        /// 소모 후 0개가 되면 라인 자체를 제거한다.
        /// </summary>
        public bool TryConsume(EquipmentSO definition, int amount)
        {
            if (!_owned.TryGetValue(definition, out OwnedEquipment owned) || owned.Count < amount)
            {
                return false;
            }

            owned.Count -= amount;

            if (owned.Count <= 0)
            {
                _owned.Remove(definition);
            }

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
