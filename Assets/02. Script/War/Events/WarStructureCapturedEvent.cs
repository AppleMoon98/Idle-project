namespace War.Events
{
    /// <summary>
    /// 구조물의 점령 게이지가 가득 차 점령이 완료되었을 때 발행되는 이벤트.
    /// </summary>
    public readonly struct WarStructureCapturedEvent
    {
        /// <summary>
        /// 점령된 구조물.
        /// </summary>
        public WarStructure Structure { get; }

        public WarStructureCapturedEvent(WarStructure structure)
        {
            Structure = structure;
        }
    }
}
