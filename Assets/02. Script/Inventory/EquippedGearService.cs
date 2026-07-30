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
    }
}
