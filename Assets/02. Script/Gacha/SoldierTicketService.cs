using Core;
using Gacha.Events;

namespace Gacha
{
    /// <summary>
    /// 보유 병사 소환권을 관리하는 서비스. CurrencyService(골드)/EnhancementStoneService(강화석)와
    /// 동일한 형태로, 소환권을 얻는 경로(보상/구매 등)는 아직 없어 소비 쪽(가챠 뽑기)만 먼저 만들어둔다.
    /// </summary>
    public sealed class SoldierTicketService : IManager, IService
    {
        private readonly EventBus _events;
        private int _currentTickets;

        /// <summary>
        /// 현재 보유 병사 소환권.
        /// </summary>
        public int CurrentTickets => _currentTickets;

        /// <summary>
        /// initialTickets: 저장된 소환권으로 시작하기 위한 초기값(SaveService.Load() 결과). 기본 0.
        /// </summary>
        public SoldierTicketService(EventBus events, int initialTickets = 0)
        {
            _events = events;
            _currentTickets = initialTickets;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 소환권을 더하고 변경 이벤트를 발행한다.
        /// </summary>
        public void AddTickets(int amount)
        {
            _currentTickets += amount;
            _events.Publish(new SoldierTicketChangedEvent(_currentTickets));
        }

        /// <summary>
        /// 소환권 소비를 시도한다. 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendTickets(int amount)
        {
            if (amount > _currentTickets)
            {
                return false;
            }

            _currentTickets -= amount;
            _events.Publish(new SoldierTicketChangedEvent(_currentTickets));
            return true;
        }
    }
}
