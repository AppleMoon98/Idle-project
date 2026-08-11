using System.Collections.Generic;
using Core;
using Gacha.Events;
using Loot;
using Soldier;

namespace Gacha
{
    /// <summary>
    /// 병사 소환권 또는 골드를 소모해 가챠를 실행하는 서비스(테이블별 CurrencyType으로 결정).
    /// SoldierSO/SoldierRosterService를 직접 참조해 새 유닛을 로스터에 추가하지만(Loot이
    /// Equipment 타입을 참조하는 것과 같은 방향), Soldier 도메인은 이 서비스의 존재를 전혀
    /// 모른다. 티어(골드/티켓/픽업 등)별로 확률 테이블이 따로 있고, 몇 번째 티어인지는
    /// 호출자(UI)가 인덱스로 지정한다 — 티어 추가는 tiers 배열에 에셋 하나 더 넣는 것만으로
    /// 끝나고 이 서비스는 손댈 필요가 없다.
    /// </summary>
    public sealed class GachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly SoldierTicketService _tickets;
        private readonly CurrencyService _currency;
        private readonly SoldierRosterService _roster;
        private readonly GachaTableSO[] _tiers;

        public GachaService(EventBus events, SoldierTicketService tickets, CurrencyService currency, SoldierRosterService roster, GachaTableSO[] tiers)
        {
            _events = events;
            _tickets = tickets;
            _currency = currency;
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
        /// tierIndex 티어로 가챠를 최대 count회 시도한다. 소환권이 모자라거나 확률 테이블에
        /// 콘텐츠가 없어 중간에 실패하면 그 시점까지 성공한 결과만 반환한다(부분 성공 허용 —
        /// "300개 뽑기"를 눌렀는데 소환권이 50개분밖에 없으면 50개만 뽑힌다). 1개 이상 성공하면
        /// SoldierPulledEvent를 한 번만 발행한다(1개 뽑기도 원소 1개짜리 목록으로 동일하게 처리).
        /// </summary>
        public IReadOnlyList<OwnedSoldier> Pull(int tierIndex, int count)
        {
            var results = new List<OwnedSoldier>();

            for (int i = 0; i < count; i++)
            {
                if (!TryPullOne(tierIndex, out OwnedSoldier result))
                {
                    break;
                }

                results.Add(result);
            }

            if (results.Count > 0)
            {
                _events.Publish(new SoldierPulledEvent(results));
            }

            return results;
        }

        private bool TryPullOne(int tierIndex, out OwnedSoldier result)
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

            bool spent = table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.TrySpendGold(table.GoldCostPerPull)
                : _tickets.TrySpendTickets(table.TicketCostPerPull);

            if (!spent)
            {
                return false;
            }

            result = _roster.AddSoldier(picked);
            return true;
        }
    }
}
