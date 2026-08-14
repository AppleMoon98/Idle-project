namespace Gacha.Events
{
    /// <summary>
    /// 보유 무기 뽑기권 수량이 변경되었을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct EquipmentGachaTicketChangedEvent
    {
        /// <summary>
        /// 변경 후 현재 보유 수량.
        /// </summary>
        public int CurrentTickets { get; }

        public EquipmentGachaTicketChangedEvent(int currentTickets)
        {
            CurrentTickets = currentTickets;
        }
    }
}
