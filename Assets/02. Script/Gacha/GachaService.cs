using Core;
using Gacha.Events;
using Soldier;

namespace Gacha
{
    /// <summary>
    /// 병사 소환권을 소모해 가챠를 실행하는 서비스. SoldierSO/SoldierRosterService를 직접
    /// 참조해 새 유닛을 로스터에 추가하지만(Loot이 Equipment 타입을 참조하는 것과 같은 방향),
    /// Soldier 도메인은 이 서비스의 존재를 전혀 모른다.
    /// </summary>
    public sealed class GachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly SoldierTicketService _tickets;
        private readonly SoldierRosterService _roster;
        private readonly GachaTableSO _table;

        public GachaService(EventBus events, SoldierTicketService tickets, SoldierRosterService roster, GachaTableSO table)
        {
            _events = events;
            _tickets = tickets;
            _roster = roster;
            _table = table;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 가챠 1회를 시도한다. 확률 테이블에서 먼저 결과를 굴려본 뒤(콘텐츠 미비로 뽑을 병사가
        /// 없으면 소환권을 소모하지 않고 false), 소환권 소비에 성공한 경우에만 실제로 로스터에
        /// 추가하고 SoldierPulledEvent를 발행한다.
        /// </summary>
        public bool TryPull(out OwnedSoldier result)
        {
            result = null;
            SoldierSO picked = GachaRoller.RollWeighted(_table.Entries);

            if (picked == null)
            {
                return false;
            }

            if (!_tickets.TrySpendTickets(_table.TicketCostPerPull))
            {
                return false;
            }

            result = _roster.AddSoldier(picked);
            _events.Publish(new SoldierPulledEvent(result));
            return true;
        }
    }
}
