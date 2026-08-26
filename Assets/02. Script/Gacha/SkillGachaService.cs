using System;
using System.Collections.Generic;
using Core;
using Gacha.Events;
using Loot;
using Skill;
using Skill.Events;
using UI.Events;
using UnityEngine;

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
        private readonly GachaGoldPullTracker _goldPullTracker;

        public SkillGachaService(EventBus events, SkillScrollService scrolls, CurrencyService currency, SkillService skills, SkillGachaTableSO[] tiers)
        {
            _events = events;
            _scrolls = scrolls;
            _currency = currency;
            _skills = skills;
            _tiers = tiers;
            _goldPullTracker = new GachaGoldPullTracker(tiers.Length);
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
            return _goldPullTracker.GetCount(tierIndex);
        }

        /// <summary>
        /// 테이블 배열 순서 그대로의 골드 뽑기 누적 횟수 스냅샷. Gacha.GachaService의 동명
        /// 메서드와 같은 용도(SaveService가 세이브 직렬화에 쓴다).
        /// </summary>
        public int[] ExportGoldPullCountsSnapshot()
        {
            return _goldPullTracker.ExportSnapshot();
        }

        /// <summary>
        /// 세이브에서 복원한 스냅샷을 그대로 되돌린다. Gacha.GachaService.RestoreGoldPullCountsSnapshot과
        /// 같은 규칙(길이 안 맞으면 겹치는 앞부분만, 이벤트 미발행).
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
        /// tierIndex 티어로 가챠를 최대 count회 시도한다. 주문서가 모자라거나 확률 테이블에
        /// 콘텐츠가 없거나(만렙 스킬 제외 후 후보가 하나도 없는 경우 포함) 중간에 실패하면
        /// 그 시점까지 성공한 결과만 반환한다(부분 성공 허용). 1개 이상 성공하면 SkillPulledEvent를
        /// 한 번만 발행한다(1개 뽑기도 원소 1개짜리 목록으로 동일하게 처리).
        /// 확률 판정/재화 소모는 시도마다(TryPullOne) 개별적으로 이뤄지지만, SkillService.AddCopy는
        /// 루프가 끝난 뒤 결과를 정의별로 집계해 서로 다른 스킬 종류 수만큼만 호출한다(GitHub 이슈
        /// #21) - 300연에서 같은 스킬이 반복 당첨될 때마다 AddCopy를 매번 부르면
        /// SkillCountChangedEvent도 매번 발행되는데, AddCopy(definition, amount)가 이미 개수
        /// 인자를 지원하므로 새 API 없이 호출 횟수만 줄일 수 있다.
        /// </summary>
        public IReadOnlyList<SkillSO> Pull(int tierIndex, int count)
        {
            if (count <= 0 || tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return Array.Empty<SkillSO>();
            }

            // 1회분조차 못 낼 잔액이면 굴려보지도 않고 안내만 하고 끝낸다.
            if (!CanAffordOnePull(_tiers[tierIndex], tierIndex))
            {
                _events.Publish(new ToastMessageRequestedEvent("재화가 모자랍니다."));
                return Array.Empty<SkillSO>();
            }

            // 후보 목록은 배치 시작 시점에 한 번만 계산한다(GitHub 이슈 #21) - 가챠는 AddCopy(보유
            // 개수만 증가)만 호출할 뿐 TryLevelUp을 호출하지 않으므로, 한 배치 안에서는 어떤 스킬도
            // IsMaxLevel 판정이 바뀔 수 없다(레벨이 바뀌어야 만렙 여부가 바뀌는데, 레벨을 바꾸는
            // 호출 자체가 이 배치 안에 없음) - 매 시도(TryPullOne)마다 다시 계산해도 얻는 정확성
            // 이득이 없어 그대로 비용만 300배가 되고 있었다.
            SkillGachaPoolEntry[] rawEntries = _tiers[tierIndex].Entries;
            List<SkillGachaPoolEntry> candidates = BuildLevelableCandidates(rawEntries);

            // 후보가 비었을 때 두 원인을 미리 구분해둔다(GitHub 이슈 #22) - 원본 엔트리 자체가
            // 비어있으면(카탈로그 미설정 등) 콘텐츠/설정 오류(NoCandidates), 원본은 있는데
            // 필터링(만렙 제외) 후에만 비었으면 정상적인 성장 완료 상태(AllCandidatesMaxed)다.
            // 배치 시작 시점에 한 번만 판정하면 충분하다 - candidates 자체가 이미 배치 내내
            // 재사용되므로(section 이슈 #21) 이 두 원인의 판정도 배치 도중 바뀔 수 없다.
            GachaPullStopReason emptyCandidatesReason = (rawEntries == null || rawEntries.Length == 0)
                ? GachaPullStopReason.NoCandidates
                : GachaPullStopReason.AllCandidatesMaxed;

            GachaPullStopReason stopReason = GachaPullStopReason.None;
            var results = new List<SkillSO>();

            for (int i = 0; i < count; i++)
            {
                if (!TryPullOne(tierIndex, candidates, emptyCandidatesReason, out SkillSO result, out stopReason))
                {
                    break;
                }

                results.Add(result);
            }

            if (results.Count > 0)
            {
                GrantByDefinition(results);
                _events.Publish(new SkillPulledEvent(results));
            }

            if (stopReason == GachaPullStopReason.NoCandidates)
            {
                Debug.LogWarning($"[SkillGachaService] tierIndex={tierIndex}의 뽑기 후보 데이터가 비어있음(카탈로그/테이블 설정 오류로 보임) - 만렙과는 다른 콘텐츠 오류 상태(GitHub 이슈 #22).");
            }

            GachaPullToast.PublishIfIncomplete(
                _events, results.Count, count, stopReason,
                noCandidatesMessage: "뽑기 콘텐츠를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.",
                allMaxedMessage: "모든 스킬이 최대 레벨입니다.");

            return results;
        }

        /// <summary>
        /// results를 정의별로 집계해(같은 스킬이 여러 번 뽑혔으면 amount로 합산) SkillService.
        /// AddCopy를 서로 다른 스킬 종류 수만큼만 호출한다 - 300연이어도 스킬 종류(현재 9종)를
        /// 넘는 호출은 일어나지 않는다.
        /// </summary>
        private void GrantByDefinition(List<SkillSO> results)
        {
            var countByDefinition = new Dictionary<SkillSO, int>();

            foreach (SkillSO definition in results)
            {
                countByDefinition.TryGetValue(definition, out int existing);
                countByDefinition[definition] = existing + 1;
            }

            foreach (KeyValuePair<SkillSO, int> entry in countByDefinition)
            {
                _skills.AddCopy(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// tierIndex 티어에 지금 당장 레벨업 가능한(만렙이 아닌) 스킬이 하나라도 있는지. UI가
        /// "전부 만렙이라 뽑아도 소용없는" 상태를 뽑기 버튼을 비활성화하는 식으로 미리 보여줄 때
        /// 쓴다(GitHub 이슈 #22) - BuildLevelableCandidates와 같은 조건이지만 목록을 만들지 않고
        /// 첫 후보를 찾는 즉시 반환해 매 프레임/이벤트마다 호출해도 저렴하다.
        /// </summary>
        public bool HasAnyLevelableCandidate(int tierIndex)
        {
            if (tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                return false;
            }

            SkillGachaPoolEntry[] entries = _tiers[tierIndex].Entries;

            if (entries == null)
            {
                return false;
            }

            foreach (SkillGachaPoolEntry entry in entries)
            {
                if (entry.Skill != null && !_skills.IsMaxLevel(entry.Skill))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// table의 소모 재화(골드 또는 스킬 주문서) 잔액이 1회분 비용 이상인지. 골드는 누적
        /// 뽑기 횟수에 따라 오르는 다음 1회 비용(GetGoldCostForPull) 기준.
        /// </summary>
        private bool CanAffordOnePull(SkillGachaTableSO table, int tierIndex)
        {
            return table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.CanAfford(table.GetGoldCostForPull(_goldPullTracker.GetCount(tierIndex)))
                : _scrolls.CurrentScrolls >= table.TicketCostPerPull;
        }

        /// <summary>
        /// emptyCandidatesReason은 Pull()이 배치 시작 시점에 미리 판정해둔, candidates가 비었을
        /// 때 쓸 이유(NoCandidates=데이터 오류 vs AllCandidatesMaxed=정상 성장 완료)다 - tierIndex
        /// 자체가 범위를 벗어난 경우는 이 판정과 무관하게 항상 NoCandidates(명백한 설정 오류)다.
        /// </summary>
        private bool TryPullOne(int tierIndex, List<SkillGachaPoolEntry> candidates, GachaPullStopReason emptyCandidatesReason, out SkillSO result, out GachaPullStopReason reason)
        {
            result = null;
            reason = GachaPullStopReason.None;

            if (tierIndex < 0 || tierIndex >= _tiers.Length)
            {
                reason = GachaPullStopReason.NoCandidates;
                return false;
            }

            SkillGachaTableSO table = _tiers[tierIndex];
            SkillSO picked = SkillGachaRoller.RollWeighted(candidates);

            if (picked == null)
            {
                reason = emptyCandidatesReason;
                return false;
            }

            bool spent = table.CurrencyType == GachaCurrencyType.Gold
                ? _currency.TrySpendGold(table.GetGoldCostForPull(_goldPullTracker.GetCount(tierIndex)))
                : _scrolls.TrySpendScrolls(table.TicketCostPerPull);

            if (!spent)
            {
                reason = GachaPullStopReason.InsufficientCurrency;
                return false;
            }

            if (table.CurrencyType == GachaCurrencyType.Gold)
            {
                _goldPullTracker.Increment(tierIndex);
            }

            result = picked;
            return true;
        }

        /// <summary>
        /// 이미 최대 레벨인 스킬을 후보에서 제외한 목록을 만든다. Pull() 배치 시작 시점에 한 번만
        /// 계산해 배치 내내 재사용한다(GitHub 이슈 #21) - 가챠는 AddCopy(보유 개수 증가)만
        /// 호출하고 TryLevelUp(레벨 자체를 올리는 것)은 절대 호출하지 않으므로, 한 배치 안에서
        /// IsMaxLevel 판정이 바뀔 수 없어 시도마다 다시 계산할 필요가 없다.
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
