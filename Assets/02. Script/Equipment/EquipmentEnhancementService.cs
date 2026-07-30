using Core;
using Inventory;

namespace Equipment
{
    /// <summary>
    /// 중복 장비와 강화석을 소모해 장비 라인의 강화 레벨을 올린다. 능력치 자체 계산/적용은
    /// 다루지 않고, EquipmentEnhancementConfigSO의 StatBonusPerLevel * 레벨을 나중에
    /// 장착 시스템이 조회해서 적용하는 방식을 염두에 둔다.
    /// </summary>
    public sealed class EquipmentEnhancementService : IManager, IService
    {
        private readonly InventoryService _inventory;
        private readonly EnhancementStoneService _stones;
        private readonly EquipmentEnhancementConfigSO _config;

        public EquipmentEnhancementService(
            InventoryService inventory,
            EnhancementStoneService stones,
            EquipmentEnhancementConfigSO config)
        {
            _inventory = inventory;
            _stones = stones;
            _config = config;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// definition의 다음 강화에 필요한 강화석 비용. 보유하고 있지 않거나 이미 최대 레벨이면 -1.
        /// </summary>
        public int GetNextStoneCost(EquipmentSO definition)
        {
            if (!_inventory.TryGet(definition, out OwnedEquipment owned) || owned.EnhancementLevel >= _config.MaxLevel)
            {
                return -1;
            }

            return _config.StoneCostBase + _config.StoneCostIncreasePerLevel * owned.EnhancementLevel;
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
    }
}
