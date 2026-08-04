using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// slot에 속한 보유 라인을 등급 낮은 것부터 순서대로, 5개가 모이는 대로 계속 합성한다.
        /// 합성 결과가 다음 등급 라인에 쌓여 그 라인도 재료가 차면 이어서 합성되도록(연쇄),
        /// 한 바퀴에서 아무 진전이 없을 때까지 재스캔을 반복한다. 실제로 성공한 합성 횟수를 반환한다.
        /// </summary>
        public int TryFuseAll(EquipmentType slot)
        {
            int successCount = 0;
            bool progressed;

            do
            {
                progressed = false;

                List<EquipmentSO> lines = _inventory.Items
                    .Where(owned => owned.Definition.EquipmentType == slot)
                    .OrderBy(owned => _gradeCatalog.IndexOf(owned.Definition.Grade))
                    .Select(owned => owned.Definition)
                    .ToList();

                foreach (EquipmentSO definition in lines)
                {
                    while (TryFuse(definition))
                    {
                        successCount++;
                        progressed = true;
                    }
                }
            } while (progressed);

            return successCount;
        }
    }
}
