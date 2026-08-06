using System.Collections.Generic;
using Core;
using Gacha.Events;
using Skill;
using Skill.Events;

namespace Gacha
{
    /// <summary>
    /// 스킬 주문서를 소모해 가챠를 실행하는 서비스. GachaService(병사)와 대칭되는 구조지만,
    /// 새 유닛/아이템을 지급하는 대신 뽑힌 스킬을 골드/강화석 없이 무료로 1레벨 올린다
    /// (SkillService.LevelUpFree) — 스킬은 이미 SkillCatalogSO에 전부 등재되어 있어 "보유"라는
    /// 개념이 없으므로, 다른 뽑기들과 달리 "아이템 획득"이 아니라 "무료 레벨업 1회 획득"으로
    /// 동작한다. 이미 최대 레벨인 스킬은 매 시도마다 후보에서 제외한다. 티어별로 확률 테이블이
    /// 따로 있고, tiers 배열에 에셋만 추가하면 새 티어가 늘어난다.
    /// </summary>
    public sealed class SkillGachaService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly SkillScrollService _scrolls;
        private readonly SkillService _skills;
        private readonly SkillGachaTableSO[] _tiers;

        public SkillGachaService(EventBus events, SkillScrollService scrolls, SkillService skills, SkillGachaTableSO[] tiers)
        {
            _events = events;
            _scrolls = scrolls;
            _skills = skills;
            _tiers = tiers;
        }

        /// <summary>
        /// 이 카테고리(스킬 뽑기)가 제공하는 티어 목록. UI가 하위 탭을 이 배열 순서대로 만든다.
        /// </summary>
        public SkillGachaTableSO[] Tiers => _tiers;

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

            if (!_scrolls.TrySpendScrolls(table.TicketCostPerPull))
            {
                return false;
            }

            _skills.LevelUpFree(picked);
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
