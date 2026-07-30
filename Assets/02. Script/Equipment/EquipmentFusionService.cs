using Core;
using Inventory;
using Loot.Events;

namespace Equipment
{
    /// <summary>
    /// 같은 슬롯·같은 등급 장비 RequiredCount개를 소모해 그 슬롯의 다음 등급 장비 1개로 합성한다.
    /// 결과물은 ItemDroppedEvent를 재발행해 InventoryService의 일반 획득 경로를 그대로 탄다.
    /// </summary>
    public sealed class EquipmentFusionService : IManager, IService
    {
        private const int RequiredCount = 5;

        private readonly EventBus _events;
        private readonly InventoryService _inventory;
        private readonly EquipmentGradeCatalogSO _gradeCatalog;
        private readonly EquipmentCatalogSO _equipmentCatalog;

        public EquipmentFusionService(
            EventBus events,
            InventoryService inventory,
            EquipmentGradeCatalogSO gradeCatalog,
            EquipmentCatalogSO equipmentCatalog)
        {
            _events = events;
            _inventory = inventory;
            _gradeCatalog = gradeCatalog;
            _equipmentCatalog = equipmentCatalog;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// definition이 이미 최고 등급이거나, 다음 등급 장비가 카탈로그에 없거나(콘텐츠 미비),
        /// 재료가 RequiredCount개 미만이면 실패한다.
        /// </summary>
        public bool TryFuse(EquipmentSO definition)
        {
            EquipmentGradeSO nextGrade = _gradeCatalog.GetNext(definition.Grade);

            if (nextGrade == null)
            {
                return false;
            }

            EquipmentSO nextItem = _equipmentCatalog.FindBySlotAndGrade(definition.EquipmentType, nextGrade);

            if (nextItem == null)
            {
                return false;
            }

            if (!_inventory.TryConsume(definition, RequiredCount))
            {
                return false;
            }

            _events.Publish(new ItemDroppedEvent(nextItem));
            return true;
        }
    }
}
