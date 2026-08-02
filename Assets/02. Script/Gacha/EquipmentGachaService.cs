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
        /// tierIndex 티어로 가챠 1회를 시도한다. 확률 테이블에서 먼저 결과를 굴려본 뒤(콘텐츠
        /// 미비로 뽑을 장비가 없으면 골드를 소모하지 않고 false), 골드 소비에 성공한 경우에만
        /// ItemDroppedEvent(인벤토리 지급)와 EquipmentPulledEvent(결과 알림)를 발행한다.
        /// </summary>
        public bool TryPull(int tierIndex, out EquipmentSO result)
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
            _events.Publish(new ItemDroppedEvent(picked));
            _events.Publish(new EquipmentPulledEvent(picked));
            return true;
        }
    }
}
