using System;
using System.Collections.Generic;
using Core;
using Enhancement;
using Equipment.Events;
using Inventory;
using Inventory.Events;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장착 중인 5슬롯 전체를 훑어 능력치별 총 보너스(EquipmentStatConfigSO의 슬롯 계수 ×
    /// EquipmentEnhancementConfigSO의 강화 배율)를 계산한다. EquippedGearService의 장착 변경과
    /// InventoryService의 보유 라인 변경(강화 레벨 상승 포함)을 구독해 재계산하고, 직전에 발행한
    /// 값과 실제로 달라진 능력치에 대해서만 EquipmentStatsChangedEvent를 발행한다.
    /// </summary>
    public sealed class EquipmentStatService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly EquippedGearService _equippedGear;
        private readonly EquipmentGradeCatalogSO _gradeCatalog;
        private readonly EquipmentEnhancementConfigSO _enhancementConfig;
        private readonly EquipmentStatConfigSO _statConfig;

        private readonly Dictionary<EnhancementStatType, float> _lastPublished = new();

        public EquipmentStatService(
            EventBus events,
            EquippedGearService equippedGear,
            EquipmentGradeCatalogSO gradeCatalog,
            EquipmentEnhancementConfigSO enhancementConfig,
            EquipmentStatConfigSO statConfig)
        {
            _events = events;
            _equippedGear = equippedGear;
            _gradeCatalog = gradeCatalog;
            _enhancementConfig = enhancementConfig;
            _statConfig = statConfig;
        }

        public void Initialize()
        {
            _events.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            _events.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            _events.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            RecomputeAndPublish();
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            RecomputeAndPublish();
        }

        /// <summary>
        /// 장착 5슬롯 전체를 다시 계산해, 이전에 발행한 값과 달라진 능력치만 이벤트로 발행한다.
        /// 세이브 복원 직후(RestoreSnapshot은 이벤트를 발행하지 않으므로) 초기 반영을 위해
        /// GameBootstrapper.Start()에서도 한 번 호출한다.
        /// </summary>
        public void RecomputeAndPublish()
        {
            var totals = new Dictionary<EnhancementStatType, float>();

            foreach (EquipmentType slot in (EquipmentType[])Enum.GetValues(typeof(EquipmentType)))
            {
                OwnedEquipment owned = _equippedGear.GetEquipped(slot);

                if (owned == null)
                {
                    continue;
                }

                int gradeIndex = _gradeCatalog.IndexOf(owned.Definition.Grade);

                foreach (EquipmentStatConfigSO.SlotStatEntry entry in _statConfig.GetEntries(slot))
                {
                    float bonus = EquipmentStatMath.CalculateBonus(entry, gradeIndex, owned.EnhancementLevel, _enhancementConfig.StatBonusPerLevel);

                    totals.TryGetValue(entry.StatType, out float existing);
                    totals[entry.StatType] = existing + bonus;
                }
            }

            foreach (EnhancementStatType statType in (EnhancementStatType[])Enum.GetValues(typeof(EnhancementStatType)))
            {
                totals.TryGetValue(statType, out float newTotal);
                _lastPublished.TryGetValue(statType, out float previousTotal);

                if (Mathf.Approximately(newTotal, previousTotal))
                {
                    continue;
                }

                _lastPublished[statType] = newTotal;
                _events.Publish(new EquipmentStatsChangedEvent(statType, newTotal));
            }
        }

        /// <summary>
        /// definition을 enhancementLevel로 착용했을 때 주는 능력치 옵션 목록을 계산한다. 실제 장착
        /// 여부와 무관하게 호출 가능 — 장비 목록 UI가 아이템을 미리보기/비교할 때 쓴다.
        /// </summary>
        public IReadOnlyList<(EnhancementStatType StatType, float Bonus)> CalculatePreview(EquipmentSO definition, int enhancementLevel)
        {
            var preview = new List<(EnhancementStatType, float)>();

            if (definition == null)
            {
                return preview;
            }

            int gradeIndex = _gradeCatalog.IndexOf(definition.Grade);

            foreach (EquipmentStatConfigSO.SlotStatEntry entry in _statConfig.GetEntries(definition.EquipmentType))
            {
                float bonus = EquipmentStatMath.CalculateBonus(entry, gradeIndex, enhancementLevel, _enhancementConfig.StatBonusPerLevel);
                preview.Add((entry.StatType, bonus));
            }

            return preview;
        }
    }
}
