using System.Collections.Generic;
using Equipment;

namespace Gacha.Events
{
    /// <summary>
    /// 무기 가챠 뽑기(1회 이상 묶음)가 성공해 새 장비가 인벤토리에 지급되었을 때 EventBus를 통해
    /// 발행되는 이벤트. SoldierPulledEvent(병사 가챠)와 대칭되는 이벤트. 1개 뽑기도 원소 1개짜리
    /// 목록으로 취급해 다다뽑기와 같은 경로를 탄다.
    /// </summary>
    public readonly struct EquipmentPulledEvent
    {
        /// <summary>
        /// 이번 뽑기로 새로 획득한 장비 정의 목록.
        /// </summary>
        public IReadOnlyList<EquipmentSO> Pulled { get; }

        public EquipmentPulledEvent(IReadOnlyList<EquipmentSO> pulled)
        {
            Pulled = pulled;
        }
    }
}
