namespace Equipment
{
    /// <summary>
    /// 장비의 착용 슬롯 종류. 실제 장착 상태는 Inventory.EquippedGearService가, 착용 시 스탯 적용은
    /// Equipment.EquipmentStatService/EquipmentStatMath가 담당한다.
    /// </summary>
    public enum EquipmentType
    {
        Weapon,
        Gloves,
        Armor,
        Helmet,
        Shoes
    }
}
