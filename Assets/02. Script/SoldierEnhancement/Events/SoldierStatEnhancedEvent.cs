using Enhancement;

namespace SoldierEnhancement.Events
{
    /// <summary>
    /// 병사 능력치 강화가 성공했을 때 EventBus를 통해 발행되는 이벤트.
    /// Enhancement.Events.StatEnhancedEvent(플레이어용)와 동일한 shape이지만, 배치된 모든 병사에게
    /// 전역 적용되는 병사 전용 강화라 별도 이벤트로 분리한다 — 같은 타입을 쓰면 Player용
    /// StatEnhancementReceiver가 병사 강화 이벤트에도 반응해버린다.
    /// </summary>
    public readonly struct SoldierStatEnhancedEvent
    {
        /// <summary>
        /// 강화된 능력치 종류.
        /// </summary>
        public EnhancementStatType StatType { get; }

        /// <summary>
        /// 이번 강화로 적용할 증가량. 구독자가 자신의 RuntimeStats에 이 값을 더한다.
        /// </summary>
        public float ValuePerLevel { get; }

        /// <summary>
        /// 강화 후 레벨.
        /// </summary>
        public int NewLevel { get; }

        public SoldierStatEnhancedEvent(EnhancementStatType statType, float valuePerLevel, int newLevel)
        {
            StatType = statType;
            ValuePerLevel = valuePerLevel;
            NewLevel = newLevel;
        }
    }
}
