using Equipment;

namespace Gacha.Events
{
    /// <summary>
    /// 무기 가챠 뽑기가 성공해 새 장비가 인벤토리에 지급되었을 때 EventBus를 통해 발행되는 이벤트.
    /// SoldierPulledEvent(병사 가챠)와 대칭되는 이벤트.
    /// </summary>
    public readonly struct EquipmentPulledEvent
    {
        /// <summary>
        /// 이번 뽑기로 새로 획득한 장비 정의.
        /// </summary>
        public EquipmentSO Pulled { get; }

        public EquipmentPulledEvent(EquipmentSO pulled)
        {
            Pulled = pulled;
        }
    }
}
