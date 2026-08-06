using System.Collections.Generic;
using Core;
using Equipment;
using Gacha.Events;
using Loot;
using Loot.Events;

namespace Gacha
{
    /// <summary>
    /// 골드를 소모해 장비 가챠를 실행하는 서비스. GachaService(병사)와 대칭되는 구조지만,
    /// 새 장비는 로스터가 아니라 InventoryService가 다루므로 직접 참조하지 않고
    /// ItemDroppedEvent를 재발행해 기존 획득 파이프라인(합성 시스템과 동일한 방식)을 그대로 탄다.
    /// 슬롯(무기/장갑/갑옷/투구/신발)별로 독립된 티어 배열을 가지며, 슬롯을 먼저 고르고 그 안에서
    /// 티어를 고르는 2단계 구조다 — 티어 배열에 에셋만 추가하면 그 슬롯의 새 티어가 늘어난다.
    /// </summary>
    public sealed class EquipmentGachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly CurrencyService _currency;
        private readonly EquipmentGachaSlotTiers[] _slots;

        public EquipmentGachaService(EventBus events, CurrencyService currency, EquipmentGachaSlotTiers[] slots)
        {
            _events = events;
            _currency = currency;
            _slots = slots;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// slot에 해당하는 티어 배열. 등록되지 않은 슬롯이면 빈 배열(콘텐츠 미비 취급).
        /// </summary>
        public EquipmentGachaTableSO[] GetTiers(EquipmentType slot)
        {
            foreach (EquipmentGachaSlotTiers entry in _slots)
            {
                if (entry.Slot == slot)
                {
                    return entry.Tiers;
                }
            }

            return System.Array.Empty<EquipmentGachaTableSO>();
        }

        /// <summary>
        /// slot의 tierIndex 티어로 가챠를 최대 count회 시도한다. 골드가 모자라거나 확률 테이블에 콘텐츠가
        /// 없어 중간에 실패하면 그 시점까지 성공한 결과만 반환한다(부분 성공 허용). 성공한 개별
        /// 아이템은 뽑히는 즉시 ItemDroppedEvent로 인벤토리에 지급되고, 1개 이상 성공하면
        /// EquipmentPulledEvent를 한 번만 발행한다(1개 뽑기도 원소 1개짜리 목록으로 동일하게 처리).
        /// </summary>
        public IReadOnlyList<EquipmentSO> Pull(EquipmentType slot, int tierIndex, int count)
        {
            var results = new List<EquipmentSO>();
            EquipmentGachaTableSO[] tiers = GetTiers(slot);

            for (int i = 0; i < count; i++)
            {
                if (!TryPullOne(tiers, tierIndex, out EquipmentSO picked))
                {
                    break;
                }

                results.Add(picked);
                _events.Publish(new ItemDroppedEvent(picked));
            }

            if (results.Count > 0)
            {
                _events.Publish(new EquipmentPulledEvent(results));
            }

            return results;
        }

        private bool TryPullOne(EquipmentGachaTableSO[] tiers, int tierIndex, out EquipmentSO result)
        {
            result = null;

            if (tierIndex < 0 || tierIndex >= tiers.Length)
            {
                return false;
            }

            EquipmentGachaTableSO table = tiers[tierIndex];
            EquipmentSO picked = EquipmentGachaRoller.RollWeighted(table.Entries);

            if (picked == null)
            {
                return false;
            }

            if (!_currency.TrySpendGold(table.GoldCostPerPull))
            {
                return false;
            }

            result = picked;
            return true;
        }
    }
}
