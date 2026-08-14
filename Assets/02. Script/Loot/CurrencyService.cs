using Core;
using Loot.Events;

namespace Loot
{
    /// <summary>
    /// 보유 골드를 관리하는 서비스. GoldEarnedEvent를 구독해 누적하고
    /// 변경 시 GoldChangedEvent를 발행해 UI 등이 구독할 수 있게 한다.
    /// BigNumber를 쓰는 이유: 방치형 특성상 장기 플레이/오프라인 보상이 쌓이면 int(약 21억)는 물론
    /// long(약 922경)도 넘어설 수 있어, 자릿수가 늘어나도 연산 비용이 일정한 가수+지수 구조가 필요하다.
    /// </summary>
    public sealed class CurrencyService : IManager, IService
    {
        private readonly EventBus _events;
        private BigNumber _currentGold;

        /// <summary>
        /// 현재 보유 골드.
        /// </summary>
        public BigNumber CurrentGold => _currentGold;

        /// <summary>
        /// initialGold: 저장된 골드로 시작하기 위한 초기값(SaveService.Load() 결과). 기본 0.
        /// </summary>
        public CurrencyService(EventBus events, BigNumber initialGold = default)
        {
            _events = events;
            _currentGold = initialGold;
        }

        public void Initialize()
        {
            _events.Subscribe<GoldEarnedEvent>(OnGoldEarned);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<GoldEarnedEvent>(OnGoldEarned);
        }

        /// <summary>
        /// 골드를 더하고 변경 이벤트를 발행한다.
        /// </summary>
        public void AddGold(BigNumber amount)
        {
            _currentGold += amount;
            _events.Publish(new GoldChangedEvent(_currentGold));
        }

        /// <summary>
        /// amount를 지금 당장 소비할 수 있는 잔액인지(실제로 소비하지는 않음). 뽑기 등에서 시도 전에
        /// "1회분도 부족한지" 미리 확인할 때 쓴다.
        /// </summary>
        public bool CanAfford(BigNumber amount)
        {
            return amount <= _currentGold;
        }

        /// <summary>
        /// 골드 소비를 시도한다. 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendGold(BigNumber amount)
        {
            if (amount > _currentGold)
            {
                return false;
            }

            _currentGold -= amount;
            _events.Publish(new GoldChangedEvent(_currentGold));
            return true;
        }

        private void OnGoldEarned(GoldEarnedEvent evt)
        {
            AddGold(evt.Amount);
        }
    }
}
