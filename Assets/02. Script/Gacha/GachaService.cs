using System.Collections.Generic;
using Core;
using Gacha.Events;
using Loot;
using Soldier;
using UI.Events;

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
        private readonly GachaGoldPullTracker _goldPullTracker;

        public GachaService(EventBus events, SoldierTicketService tickets, CurrencyService currency, SoldierRosterService roster, GachaTableSO[] tiers)
        {
            _events = events;
            _tickets = tickets;
            _currency = currency;
            _roster = roster;
            _tiers = tiers;
            _goldPullTracker = new GachaGoldPullTracker(tiers.Length);
        }

        /// <summary>
        /// 이 카테고리(병사 뽑기)가 제공하는 티어 목록. UI가 하위 탭을 이 배열 순서대로 만든다.
        /// </summary>
        public GachaTableSO[] Tiers => _tiers;

        /// <summary>
        /// tierIndex 테이블에서 지금까지 성공한 골드 뽑기 횟수(costIncrementTiers 계산용, UI가
        /// "다음 1회 비용" 표시에도 이 값 기반 GetGoldCostForPull을 함께 쓴다).
        /// </summary>
        public int GetGoldPullCount(int tierIndex)
        {
            return _goldPullTracker.GetCount(tierIndex);
        }

        /// <summary>
        /// 테이블 배열 순서 그대로의 골드 뽑기 누적 횟수 스냅샷(SaveService가 세이브 직렬화에 쓴다).
        /// </summary>
        public int[] ExportGoldPullCountsSnapshot()
        {
            return _goldPullTracker.ExportSnapshot();
        }

        /// <summary>
        /// 세이브에서 복원한 스냅샷을 그대로 되돌린다. counts가 null이거나 길이가 안 맞으면
        /// 겹치는 앞부분만 복원한다(콘텐츠 추가로 티어 수가 늘어난 세이브도 안전하게 처리).
        /// 시딩이지 게임플레이 변화가 아니므로 이벤트는 발행하지 않는다.
        /// </summary>
        public void RestoreGoldPullCountsSnapshot(int[] counts)
        {
            _goldPullTracker.RestoreSnapshot(counts);
        }

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
        /// 요청한 횟수보다 적게 실행됐다면(0회 포함) GachaPullToast로 몇 회가, 왜 안 됐는지
        /// 토스트를 함께 띄운다(GitHub 이슈 #22 - 예전엔 조용히 break만 하고 끝났다).
        /// </summary>
        public IReadOnlyList<OwnedSoldier> Pull(int tierIndex, int count)
        {
            var results = new List<OwnedSoldier>();

            if (count <= 0 || tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return results;
            }

            // 1회분조차 못 낼 잔액이면 굴려보지도 않고 안내만 하고 끝낸다 - "N회 뽑기"를 눌렀는데
            // 조용히 0개만 나오는 것보다 명확하다.
            if (!CanAffordOnePull(_tiers[tierIndex], tierIndex))
            {
                _events.Publish(new ToastMessageRequestedEvent("재화가 모자랍니다."));
                return results;
            }

            GachaPullStopReason stopReason = GachaPullStopReason.None;

            for (int i = 0; i < count; i++)
            {
                if (!TryPullOne(tierIndex, out OwnedSoldier result, out stopReason))
                {
                    break;
                }

                results.Add(result);
            }

            if (results.Count > 0)
            {
                _events.Publish(new SoldierPulledEvent(results));
            }

            GachaPullToast.PublishIfIncomplete(_events, results.Count, count, stopReason, "뽑을 수 있는 병사가 없습니다.");

            return results;
        }

        /// <summary>
        /// table의 소모 재화(골드 또는 소환권) 잔액이 1회분 비용 이상인지. 골드는 누적 뽑기
        /// 횟수에 따라 오르는 다음 1회 비용(GetGoldCostForPull) 기준.
        /// </summary>
        private bool CanAffordOnePull(GachaTableSO table, int tierIndex)
        {
            return table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.CanAfford(table.GetGoldCostForPull(_goldPullTracker.GetCount(tierIndex)))
                : _tickets.CurrentTickets >= table.TicketCostPerPull;
        }

        private bool TryPullOne(int tierIndex, out OwnedSoldier result, out GachaPullStopReason reason)
        {
            result = null;
            reason = GachaPullStopReason.None;

            if (tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                reason = GachaPullStopReason.NoCandidates;
                return false;
            }

            GachaTableSO table = _tiers[tierIndex];
            SoldierSO picked = GachaRoller.RollWeighted(table.Entries);

            if (picked == null)
            {
                reason = GachaPullStopReason.NoCandidates;
                return false;
            }

            bool spent = table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.TrySpendGold(table.GetGoldCostForPull(_goldPullTracker.GetCount(tierIndex)))
                : _tickets.TrySpendTickets(table.TicketCostPerPull);

            if (!spent)
            {
                reason = GachaPullStopReason.InsufficientCurrency;
                return false;
            }

            if (table.CurrencyType == GachaCurrencyType.Gold)
            {
                _goldPullTracker.Increment(tierIndex);
            }

            result = _roster.AddSoldier(picked);
            return true;
        }
    }
}
