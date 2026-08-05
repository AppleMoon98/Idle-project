using System;
using System.Collections.Generic;
using Core;
using Inventory;

namespace Equipment
{
    /// <summary>
    /// 중복 장비와 강화석을 소모해 장비 라인의 강화 레벨을 올린다. 능력치 자체 계산/적용은
    /// 다루지 않고, EquipmentEnhancementConfigSO의 StatBonusPerLevel * 레벨을 Equipment.EquipmentStatService가
    /// 조회해서 적용한다.
    /// </summary>
    public sealed class EquipmentEnhancementService : IManager, IService
    {
        private readonly InventoryService _inventory;
        private readonly EnhancementStoneService _stones;
        private readonly EquipmentEnhancementConfigSO _config;
        private readonly EquipmentGradeCatalogSO _gradeCatalog;

        public EquipmentEnhancementService(
            InventoryService inventory,
            EnhancementStoneService stones,
            EquipmentEnhancementConfigSO config,
            EquipmentGradeCatalogSO gradeCatalog)
        {
            _inventory = inventory;
            _stones = stones;
            _config = config;
            _gradeCatalog = gradeCatalog;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 강화 1회당 소모되는 중복 장비 개수(강화 대상 1개는 남기고 소모). UI가 필요 재료를 표시할 때 사용.
        /// </summary>
        public int DuplicatesRequiredPerLevel => _config.DuplicatesRequiredPerLevel;

        /// <summary>
        /// 최대 강화 레벨. UI가 "MAX" 여부를 판단할 때 사용.
        /// </summary>
        public int MaxLevel => _config.MaxLevel;

        /// <summary>
        /// definition의 다음 강화에 필요한 강화석 비용. 보유하고 있지 않거나 이미 최대 레벨이면 -1.
        /// </summary>
        public int GetNextStoneCost(EquipmentSO definition)
        {
            if (!_inventory.TryGet(definition, out OwnedEquipment owned) || owned.EnhancementLevel >= _config.MaxLevel)
            {
                return -1;
            }

            // int 곱셈을 그대로 두면 MaxLevel/StoneCostIncreasePerLevel이 커질 때 Enhancement.EnhancementService가
            // 이미 한 번 겪었던 것과 같은 오버플로(비용이 음수가 되어 강화가 사실상 공짜가 되는) 버그가
            // 재발할 수 있다 - long으로 계산 후 int 범위로 saturate한다.
            long cost = (long)_config.StoneCostBase + (long)_config.StoneCostIncreasePerLevel * owned.EnhancementLevel;
            return (int)Math.Min(cost, int.MaxValue);
        }

        /// <summary>
        /// definition을 1강화한다. 최대 레벨이거나, 중복 장비(강화 대상 1개 제외)가 부족하거나,
        /// 강화석이 부족하면 실패한다.
        /// </summary>
        public bool TryEnhance(EquipmentSO definition)
        {
            if (!_inventory.TryGet(definition, out OwnedEquipment owned) || owned.EnhancementLevel >= _config.MaxLevel)
            {
                return false;
            }

            int duplicatesRequired = _config.DuplicatesRequiredPerLevel;

            if (owned.Count < duplicatesRequired + 1)
            {
                return false;
            }

            int stoneCost = GetNextStoneCost(definition);

            if (!_stones.TrySpendStones(stoneCost))
            {
                return false;
            }

            _inventory.TryConsume(definition, duplicatesRequired);
            _inventory.AddEnhancementLevel(definition, 1);

            return true;
        }

        /// <summary>
        /// slot에 속한 보유 라인을 등급 낮은 것부터 순서대로, 재료(중복 장비+강화석)가
        /// 허락하는 한 각 라인을 최대 레벨까지 반복 강화한다. 실제로 성공한 강화 횟수를 반환한다.
        /// </summary>
        public int TryEnhanceAll(EquipmentType slot)
        {
            int successCount = 0;

            List<EquipmentSO> lines = _inventory.GetLinesBySlotSortedByGrade(slot, _gradeCatalog);

            foreach (EquipmentSO definition in lines)
            {
                while (TryEnhance(definition))
                {
                    successCount++;
                }
            }

            return successCount;
        }
    }
}
