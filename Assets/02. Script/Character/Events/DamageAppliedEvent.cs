using UnityEngine;

namespace Character.Events
{
    /// <summary>
    /// 캐릭터에게 데미지가 실제로 적용됐을 때(사망/힐과 구분) EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct DamageAppliedEvent
    {
        /// <summary>
        /// 데미지를 받은 캐릭터.
        /// </summary>
        public GameObject Target { get; }

        /// <summary>
        /// 적용된 데미지 양.
        /// </summary>
        public float Amount { get; }

        /// <summary>
        /// 치명타 여부.
        /// </summary>
        public bool IsCritical { get; }

        public DamageAppliedEvent(GameObject target, float amount, bool isCritical)
        {
            Target = target;
            Amount = amount;
            IsCritical = isCritical;
        }
    }
}
