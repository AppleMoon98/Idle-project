using Core;
using Loot.Events;

namespace Loot
{
    /// <summary>
    /// 보유 골드를 관리하는 서비스. GoldEarnedEvent를 구독해 누적하고
    /// 변경 시 GoldChangedEvent를 발행해 UI 등이 구독할 수 있게 한다.
    /// </summary>
    public sealed class CurrencyService : IManager, IService
    {
        private readonly EventBus _events;
        private int _currentGold;

        /// <summary>
        /// 현재 보유 골드.
        /// </summary>
        public int CurrentGold => _currentGold;

        public CurrencyService(EventBus events)
        {
            _events = events;
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
        public void AddGold(int amount)
        {
            _currentGold += amount;
            _events.Publish(new GoldChangedEvent(_currentGold));
        }

        private void OnGoldEarned(GoldEarnedEvent evt)
        {
            AddGold(evt.Amount);
        }
    }
}
