using Equipment;

namespace Inventory
{
    /// <summary>
    /// 플레이어가 보유 중인 장비 한 "라인"의 상태. 같은 EquipmentSO를 여러 개 보유해도
    /// 개별 인스턴스가 아니라 이 하나의 스택(Count)으로 관리되고, 강화 레벨도 라인 전체가 공유한다
    /// (합성/강화 재료로 소모되는 건 이 Count일 뿐, 강화된 개별 카피가 따로 존재하지 않는다).
    /// </summary>
    public sealed class OwnedEquipment
    {
        /// <summary>
        /// 이 라인이 어떤 장비 원형인지.
        /// </summary>
        public EquipmentSO Definition { get; }

        /// <summary>
        /// 현재 보유 개수(합성/강화 재료 및 UI의 "xN" 표시에 쓰인다).
        /// </summary>
        public int Count { get; internal set; }

        /// <summary>
        /// 강화 레벨. 같은 라인의 모든 보유분이 공유한다.
        /// </summary>
        public int EnhancementLevel { get; internal set; }

        public OwnedEquipment(EquipmentSO definition, int count, int enhancementLevel)
        {
            Definition = definition;
            Count = count;
            EnhancementLevel = enhancementLevel;
        }
    }
}
