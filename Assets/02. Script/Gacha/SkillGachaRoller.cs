using System.Collections.Generic;
using Skill;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 가챠 확률 테이블에서 가중치 기반으로 스킬 하나를 뽑는 순수 굴림 로직. GachaRoller(병사)와
    /// 동일한 형태로 도메인별로 그대로 복제한다(section AM의 "병행 서비스는 공유/추상화하지 않는다"
    /// 컨벤션).
    /// </summary>
    public static class SkillGachaRoller
    {
        /// <summary>
        /// entries의 가중치 합 대비 비율로 스킬 하나를 뽑는다. entries가 비어있거나 가중치 합이
        /// 0 이하이면(콘텐츠 미비) null.
        /// </summary>
        public static SkillSO RollWeighted(IReadOnlyList<SkillGachaPoolEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;

            foreach (SkillGachaPoolEntry entry in entries)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (SkillGachaPoolEntry entry in entries)
            {
                cumulative += entry.Weight;

                if (roll < cumulative)
                {
                    return entry.Skill;
                }
            }

            return null;
        }
    }
}
