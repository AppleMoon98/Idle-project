using System.Collections.Generic;
using Core;
using Equipment;
using Gacha.Events;
using Loot;
using Loot.Events;

namespace Gacha
{
    /// <summary>
    /// 골드를 소모해 무기 가챠를 실행하는 서비스. GachaService(병사)와 대칭되는 구조지만,
    /// 새 장비는 로스터가 아니라 InventoryService가 다루므로 직접 참조하지 않고
    /// ItemDroppedEvent를 재발행해 기존 획득 파이프라인(합성 시스템과 동일한 방식)을 그대로 탄다.
    /// 티어별로 확률 테이블이 따로 있고, tiers 배열에 에셋만 추가하면 새 티어가 늘어난다.
    /// </summary>
    public sealed class EquipmentGachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly CurrencyService _currency;
        private readonly EquipmentGachaTableSO[] _tiers;

        public EquipmentGachaService(EventBus events, CurrencyService currency, EquipmentGachaTableSO[] tiers)
        {
            _events = events;
            _currency = currency;
            _tiers = tiers;
        }

        /// <summary>
        /// 이 카테고리(무기 뽑기)가 제공하는 티어 목록. UI가 하위 탭을 이 배열 순서대로 만든다.
        /// </summary>
        public EquipmentGachaTableSO[] Tiers => _tiers;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// tierIndex 티어로 가챠를 최대 count회 시도한다. 골드가 모자라거나 확률 테이블에 콘텐츠가
        /// 없어 중간에 실패하면 그 시점까지 성공한 결과만 반환한다(부분 성공 허용). 성공한 개별
        /// 아이템은 뽑히는 즉시 ItemDroppedEvent로 인벤토리에 지급되고, 1개 이상 성공하면
        /// EquipmentPulledEvent를 한 번만 발행한다(1개 뽑기도 원소 1개짜리 목록으로 동일하게 처리).
        /// </summary>
        public IReadOnlyList<EquipmentSO> Pull(int tierIndex, int count)
        {
            var results = new List<EquipmentSO>();

            for (int i = 0; i < count; i++)
            {
                if (!TryPullOne(tierIndex, out EquipmentSO picked))
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

        private bool TryPullOne(int tierIndex, out EquipmentSO result)
        {
            result = null;

            if (tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return false;
            }

            EquipmentGachaTableSO table = _tiers[tierIndex];
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
