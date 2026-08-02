using System.Collections.Generic;
using Soldier;

namespace Gacha.Events
{
    /// <summary>
    /// 가챠 뽑기(1회 이상 묶음)가 성공해 새 병사 유닛이 로스터에 추가되었을 때 EventBus를 통해
    /// 발행되는 이벤트. 1개 뽑기도 원소 1개짜리 목록으로 취급해 다다뽑기와 같은 경로를 탄다.
    /// </summary>
    public readonly struct SoldierPulledEvent
    {
        /// <summary>
        /// 이번 뽑기로 새로 획득한 유닛 목록.
        /// </summary>
        public IReadOnlyList<OwnedSoldier> Pulled { get; }

        public SoldierPulledEvent(IReadOnlyList<OwnedSoldier> pulled)
        {
            Pulled = pulled;
        }
    }
}
