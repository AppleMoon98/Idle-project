namespace SoldierEquipment
{
    /// <summary>
    /// 보유 중인 병사 전용 장비 한 "라인". Inventory.OwnedEquipment와 동일한 스택형 개념이지만,
    /// 강화 레벨이 없다(등급/강화 루프가 아직 없는 도메인이므로).
    /// </summary>
    public sealed class OwnedSoldierEquipment
    {
        /// <summary>
        /// 이 라인이 어떤 장비 원형인지.
        /// </summary>
        public SoldierEquipmentSO Definition { get; }

        /// <summary>
        /// 현재 보유 개수.
        /// </summary>
        public int Count { get; internal set; }

        public OwnedSoldierEquipment(SoldierEquipmentSO definition, int count)
        {
            Definition = definition;
            Count = count;
        }
    }
}
