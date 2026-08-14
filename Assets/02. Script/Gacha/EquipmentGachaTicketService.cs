using Core;
using Gacha.Events;

namespace Gacha
{
    /// <summary>
    /// 보유 무기 뽑기권을 관리하는 서비스. SoldierTicketService/SkillScrollService와 완전히 동일한
    /// 형태 - 몬스터 처치 시 극희귀 확률로 지급되고(Gacha.RareGachaTicketDropService), 무기 뽑기
    /// 팝업의 "티켓 뽑기" 티어에서 소비된다(EquipmentGachaService.PullWithTicket).
    /// </summary>
    public sealed class EquipmentGachaTicketService : IManager, IService
    {
        private readonly EventBus _events;
        private int _currentTickets;

        /// <summary>
        /// 현재 보유 무기 뽑기권.
        /// </summary>
        public int CurrentTickets => _currentTickets;

        /// <summary>
        /// initialTickets: 저장된 뽑기권으로 시작하기 위한 초기값(SaveService.Load() 결과). 기본 0.
        /// </summary>
        public EquipmentGachaTicketService(EventBus events, int initialTickets = 0)
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
        /// 뽑기권을 더하고 변경 이벤트를 발행한다.
        /// </summary>
        public void AddTickets(int amount)
        {
            _currentTickets += amount;
            _events.Publish(new EquipmentGachaTicketChangedEvent(_currentTickets));
        }

        /// <summary>
        /// 뽑기권 소비를 시도한다. 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendTickets(int amount)
        {
            if (amount > _currentTickets)
            {
                return false;
            }

            _currentTickets -= amount;
            _events.Publish(new EquipmentGachaTicketChangedEvent(_currentTickets));
            return true;
        }
    }
}
