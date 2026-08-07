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
    /// 보유 중인(장착 여부 무관) 장비 라인 전체를 훑어 능력치별 총 보너스(EquipmentPossessionConfigSO의
    /// 슬롯 계수 × EquipmentEnhancementConfigSO의 강화 배율)를 계산한다. EquipmentStatService(장착 시
    /// 효과)와 형태는 같지만, "장착 슬롯 5개"가 아니라 "보유 중인 모든 라인"을 대상으로 슬롯별로
    /// 합산한다 — 같은 슬롯을 여러 등급으로 동시에 보유하면 전부 더해진다(사용자 결정: 라인 전체 합산).
    /// InventoryService의 보유 라인 변경(획득/소모/강화 레벨 상승)을 구독해 재계산하고, 직전에
    /// 발행한 값과 실제로 달라진 능력치에 대해서만 EquipmentPossessionStatsChangedEvent를 발행한다.
    /// </summary>
    public sealed class EquipmentPossessionService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly InventoryService _inventory;
        private readonly EquipmentGradeCatalogSO _gradeCatalog;
        private readonly EquipmentPossessionConfigSO _possessionConfig;

        private readonly Dictionary<EnhancementStatType, float> _lastPublished = new();

        public EquipmentPossessionService(
            EventBus events,
            InventoryService inventory,
            EquipmentGradeCatalogSO gradeCatalog,
            EquipmentPossessionConfigSO possessionConfig)
        {
            _events = events;
            _inventory = inventory;
            _gradeCatalog = gradeCatalog;
            _possessionConfig = possessionConfig;
        }

        public void Initialize()
        {
            _events.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            RecomputeAndPublish();
        }

        /// <summary>
        /// 보유 중인 라인 전체를 다시 계산해, 이전에 발행한 값과 달라진 능력치만 이벤트로 발행한다.
        /// 세이브 복원 직후(RestoreSnapshot은 이벤트를 발행하지 않으므로) 초기 반영을 위해
        /// GameBootstrapper.Start()에서도 한 번 호출한다.
        /// </summary>
        public void RecomputeAndPublish()
        {
            var totals = new Dictionary<EnhancementStatType, float>();

            foreach (OwnedEquipment owned in _inventory.Items)
            {
                int gradeIndex = Mathf.Max(_gradeCatalog.IndexOf(owned.Definition.Grade), 0);

                foreach (EquipmentStatConfigSO.SlotStatEntry entry in _possessionConfig.GetEntries(owned.Definition.EquipmentType))
                {
                    float bonus = CalculateBonus(entry, gradeIndex, owned.EnhancementLevel);

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
                _events.Publish(new EquipmentPossessionStatsChangedEvent(statType, newTotal));
            }
        }

        /// <summary>
        /// definition을 enhancementLevel로 보유했을 때 이 한 라인이 주는 보유 효과(퍼센트) 목록을
        /// 계산한다. 실제 보유 여부와 무관하게 호출 가능 — 장비 상세 팝업의 미리보기에서 쓴다.
        /// </summary>
        public IReadOnlyList<(EnhancementStatType StatType, float PercentBonus)> CalculatePreview(EquipmentSO definition, int enhancementLevel)
        {
            var preview = new List<(EnhancementStatType, float)>();

            if (definition == null)
            {
                return preview;
            }

            int gradeIndex = Mathf.Max(_gradeCatalog.IndexOf(definition.Grade), 0);

            foreach (EquipmentStatConfigSO.SlotStatEntry entry in _possessionConfig.GetEntries(definition.EquipmentType))
            {
                float bonus = CalculateBonus(entry, gradeIndex, enhancementLevel);
                preview.Add((entry.StatType, bonus));
            }

            return preview;
        }

        /// <summary>
        /// 보유 효과 전용 계산식. 등급 배율은 EquipmentPossessionConfigSO.GetMainGradeMultiplier(대분류
        /// 단위 계단식)로 곱하고, 강화 레벨 배율은 그 등급이 속한 대분류의 GetEnhancementTier가 정한
        /// 레벨당 증가율·레벨 상한을 적용한다(예: 슈퍼레어까지는 1%/100강, 에픽부터는 10%/20강).
        /// </summary>
        private float CalculateBonus(EquipmentStatConfigSO.SlotStatEntry entry, int gradeIndex, int enhancementLevel)
        {
            float gradeMultiplier = _possessionConfig.GetMainGradeMultiplier(gradeIndex);

            int mainGradeTier = _possessionConfig.GetMainGradeTier(gradeIndex);
            EquipmentPossessionConfigSO.PossessionEnhancementTier enhancementTier = _possessionConfig.GetEnhancementTier(mainGradeTier);
            int cappedLevel = Mathf.Min(enhancementLevel, enhancementTier.MaxLevel);
            float levelMultiplier = 1f + enhancementTier.PercentPerLevel * cappedLevel;

            return entry.BaseValue * gradeMultiplier * levelMultiplier;
        }
    }
}
