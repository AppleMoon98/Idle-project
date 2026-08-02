using Core;
using Gacha.Events;
using Soldier;

namespace Gacha
{
    /// <summary>
    /// 병사 소환권을 소모해 가챠를 실행하는 서비스. SoldierSO/SoldierRosterService를 직접
    /// 참조해 새 유닛을 로스터에 추가하지만(Loot이 Equipment 타입을 참조하는 것과 같은 방향),
    /// Soldier 도메인은 이 서비스의 존재를 전혀 모른다. 티어(일반/고급/유료 등)별로 확률 테이블이
    /// 따로 있고, 몇 번째 티어인지는 호출자(UI)가 인덱스로 지정한다 — 티어 추가는 tiers 배열에
    /// 에셋 하나 더 넣는 것만으로 끝나고 이 서비스는 손댈 필요가 없다.
    /// </summary>
    public sealed class GachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly SoldierTicketService _tickets;
        private readonly SoldierRosterService _roster;
        private readonly GachaTableSO[] _tiers;

        public GachaService(EventBus events, SoldierTicketService tickets, SoldierRosterService roster, GachaTableSO[] tiers)
        {
            _events = events;
            _tickets = tickets;
            _roster = roster;
            _tiers = tiers;
        }

        /// <summary>
        /// 이 카테고리(병사 뽑기)가 제공하는 티어 목록. UI가 하위 탭을 이 배열 순서대로 만든다.
        /// </summary>
        public GachaTableSO[] Tiers => _tiers;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// tierIndex 티어로 가챠 1회를 시도한다. 확률 테이블에서 먼저 결과를 굴려본 뒤(콘텐츠
        /// 미비로 뽑을 병사가 없으면 소환권을 소모하지 않고 false), 소환권 소비에 성공한 경우에만
        /// 실제로 로스터에 추가하고 SoldierPulledEvent를 발행한다.
        /// </summary>
        public bool TryPull(int tierIndex, out OwnedSoldier result)
        {
            result = null;

            if (tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return false;
            }

            GachaTableSO table = _tiers[tierIndex];
            SoldierSO picked = GachaRoller.RollWeighted(table.Entries);

            if (picked == null)
            {
                return false;
            }

            if (!_tickets.TrySpendTickets(table.TicketCostPerPull))
            {
                return false;
            }

            result = _roster.AddSoldier(picked);
            _events.Publish(new SoldierPulledEvent(result));
            return true;
        }
    }
}
