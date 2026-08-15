using System.Collections.Generic;
using Core;
using Gacha.Events;
using Loot;
using Skill;
using Skill.Events;
using UI.Events;

namespace Gacha
{
    /// <summary>
    /// 스킬 주문서 또는 골드를 소모해 가챠를 실행하는 서비스(테이블별 CurrencyType으로 결정).
    /// GachaService(병사)와 대칭되는 구조로,
    /// 뽑힌 스킬의 보유 개수를 1 늘린다(SkillService.AddCopy) — 스킬은 이미 SkillCatalogSO에
    /// 전부 등재되어 있어 새 슬롯을 만들 필요는 없지만, 그 보유 개수가 레벨업(SkillService.TryLevelUp)의
    /// 재료로 소모된다는 점에서 장비의 "보유 스택"과 같은 역할을 한다. 이미 최대 레벨인 스킬은
    /// 매 시도마다 후보에서 제외한다(더 모아도 레벨업할 곳이 없으므로). 티어별로 확률 테이블이
    /// 따로 있고, tiers 배열에 에셋만 추가하면 새 티어가 늘어난다.
    /// </summary>
    public sealed class SkillGachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly SkillScrollService _scrolls;
        private readonly CurrencyService _currency;
        private readonly SkillService _skills;
        private readonly SkillGachaTableSO[] _tiers;
        private readonly int[] _goldPullCounts;

        public SkillGachaService(EventBus events, SkillScrollService scrolls, CurrencyService currency, SkillService skills, SkillGachaTableSO[] tiers)
        {
            _events = events;
            _scrolls = scrolls;
            _currency = currency;
            _skills = skills;
            _tiers = tiers;
            _goldPullCounts = new int[tiers.Length];
        }

        /// <summary>
        /// 이 카테고리(스킬 뽑기)가 제공하는 티어 목록. UI가 하위 탭을 이 배열 순서대로 만든다.
        /// </summary>
        public SkillGachaTableSO[] Tiers => _tiers;

        /// <summary>
        /// tierIndex 테이블에서 지금까지 성공한 골드 뽑기 횟수. Gacha.GachaService.GetGoldPullCount와
        /// 같은 용도.
        /// </summary>
        public int GetGoldPullCount(int tierIndex)
        {
            return tierIndex >= 0 && tierIndex < _goldPullCounts.Length ? _goldPullCounts[tierIndex] : 0;
        }

        /// <summary>
        /// 테이블 배열 순서 그대로의 골드 뽑기 누적 횟수 스냅샷. Gacha.GachaService의 동명
        /// 메서드와 같은 용도(SaveService가 세이브 직렬화에 쓴다).
        /// </summary>
        public int[] ExportGoldPullCountsSnapshot()
        {
            return (int[])_goldPullCounts.Clone();
        }

        /// <summary>
        /// 세이브에서 복원한 스냅샷을 그대로 되돌린다. Gacha.GachaService.RestoreGoldPullCountsSnapshot과
        /// 같은 규칙(길이 안 맞으면 겹치는 앞부분만, 이벤트 미발행).
        /// </summary>
        public void RestoreGoldPullCountsSnapshot(int[] counts)
        {
            if (counts == null)
            {
                return;
            }

            int length = System.Math.Min(counts.Length, _goldPullCounts.Length);

            for (int i = 0; i < length; i++)
            {
                _goldPullCounts[i] = counts[i];
            }
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// tierIndex 티어로 가챠를 최대 count회 시도한다. 주문서가 모자라거나 확률 테이블에
        /// 콘텐츠가 없거나(만렙 스킬 제외 후 후보가 하나도 없는 경우 포함) 중간에 실패하면
        /// 그 시점까지 성공한 결과만 반환한다(부분 성공 허용). 1개 이상 성공하면 SkillPulledEvent를
        /// 한 번만 발행한다(1개 뽑기도 원소 1개짜리 목록으로 동일하게 처리).
        /// </summary>
        public IReadOnlyList<SkillSO> Pull(int tierIndex, int count)
        {
            var results = new List<SkillSO>();

            if (count <= 0 || tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return results;
            }

            // 1회분조차 못 낼 잔액이면 굴려보지도 않고 안내만 하고 끝낸다.
            if (!CanAffordOnePull(_tiers[tierIndex], tierIndex))
            {
                _events.Publish(new ToastMessageRequestedEvent("재화가 모자랍니다."));
                return results;
            }

            for (int i = 0; i < count; i++)
            {
                if (!TryPullOne(tierIndex, out SkillSO result))
                {
                    break;
                }

                results.Add(result);
            }

            if (results.Count > 0)
            {
                _events.Publish(new SkillPulledEvent(results));
            }

            return results;
        }

        /// <summary>
        /// table의 소모 재화(골드 또는 스킬 주문서) 잔액이 1회분 비용 이상인지. 골드는 누적
        /// 뽑기 횟수에 따라 오르는 다음 1회 비용(GetGoldCostForPull) 기준.
        /// </summary>
        private bool CanAffordOnePull(SkillGachaTableSO table, int tierIndex)
        {
            return table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.CanAfford(table.GetGoldCostForPull(_goldPullCounts[tierIndex]))
                : _scrolls.CurrentScrolls >= table.TicketCostPerPull;
        }

        private bool TryPullOne(int tierIndex, out SkillSO result)
        {
            result = null;

            if (tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return false;
            }

            SkillGachaTableSO table = _tiers[tierIndex];
            List<SkillGachaPoolEntry> candidates = BuildLevelableCandidates(table.Entries);
            SkillSO picked = SkillGachaRoller.RollWeighted(candidates);

            if (picked == null)
            {
                return false;
            }

            bool spent = table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.TrySpendGold(table.GetGoldCostForPull(_goldPullCounts[tierIndex]))
                : _scrolls.TrySpendScrolls(table.TicketCostPerPull);

            if (!spent)
            {
                return false;
            }

            if (table.CurrencyType == GachaCurrencyType.Gold)
            {
                _goldPullCounts[tierIndex]++;
            }

            _skills.AddCopy(picked);
            result = picked;
            return true;
        }

        /// <summary>
        /// 이미 최대 레벨인 스킬을 후보에서 제외한 목록을 만든다. 매 시도마다 새로 계산해야
        /// 이번 뽑기로 방금 만렙에 도달한 스킬도 바로 다음 시도에서 제외된다.
        /// </summary>
        private List<SkillGachaPoolEntry> BuildLevelableCandidates(SkillGachaPoolEntry[] entries)
        {
            var candidates = new List<SkillGachaPoolEntry>();

            if (entries == null)
            {
                return candidates;
            }

            foreach (SkillGachaPoolEntry entry in entries)
            {
                if (entry.Skill != null && !_skills.IsMaxLevel(entry.Skill))
                {
                    candidates.Add(entry);
                }
            }

            return candidates;
        }
    }
}
