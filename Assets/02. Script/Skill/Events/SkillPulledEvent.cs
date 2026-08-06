using System.Collections.Generic;

namespace Skill.Events
{
    /// <summary>
    /// 스킬 뽑기(1회 이상 묶음)가 성공해 스킬이 무료로 레벨업되었을 때 EventBus를 통해
    /// 발행되는 이벤트. Gacha.Events.SoldierPulledEvent/EquipmentPulledEvent와 동일한 형태 —
    /// 1개 뽑기도 원소 1개짜리 목록으로 취급해 다다뽑기와 같은 경로를 탄다.
    /// </summary>
    public readonly struct SkillPulledEvent
    {
        /// <summary>
        /// 이번 뽑기로 레벨업된 스킬 목록(순서대로, 같은 스킬이 여러 번 나올 수 있다).
        /// </summary>
        public IReadOnlyList<SkillSO> Pulled { get; }

        public SkillPulledEvent(IReadOnlyList<SkillSO> pulled)
        {
            Pulled = pulled;
        }
    }
}
