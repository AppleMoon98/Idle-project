using Soldier;

namespace Gacha.Events
{
    /// <summary>
    /// 가챠 뽑기가 성공해 새 병사 유닛이 로스터에 추가되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct SoldierPulledEvent
    {
        /// <summary>
        /// 이번 뽑기로 새로 획득한 유닛.
        /// </summary>
        public OwnedSoldier Pulled { get; }

        public SoldierPulledEvent(OwnedSoldier pulled)
        {
            Pulled = pulled;
        }
    }
}
