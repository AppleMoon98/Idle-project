using System.Collections.Generic;
using Core;
using Equipment;
using Inventory.Events;
using Loot.Events;

namespace Inventory
{
    /// <summary>
    /// 드롭된 장비를 보관하는 서비스. ItemDroppedEvent를 구독해 목록에 추가하고
    /// 변경 시 InventoryChangedEvent를 발행해 UI 등이 구독할 수 있게 한다.
    /// </summary>
    public sealed class InventoryService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly List<EquipmentSO> _items = new();

        /// <summary>
        /// 현재 보관 중인 장비 목록 (읽기 전용).
        /// </summary>
        public IReadOnlyList<EquipmentSO> Items => _items;

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

        private void OnItemDropped(ItemDroppedEvent evt)
        {
            _items.Add(evt.Equipment);
            _events.Publish(new InventoryChangedEvent(evt.Equipment, _items.Count));
        }
    }
}
