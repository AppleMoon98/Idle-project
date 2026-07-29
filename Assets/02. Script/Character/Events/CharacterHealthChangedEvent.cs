using UnityEngine;

namespace Character.Events
{
    /// <summary>
    /// 캐릭터의 체력이 변화했을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct CharacterHealthChangedEvent
    {
        /// <summary>
        /// 체력이 변화한 캐릭터.
        /// </summary>
        public GameObject Character { get; }

        /// <summary>
        /// 변화 후 현재 체력.
        /// </summary>
        public float Current { get; }

        /// <summary>
        /// 최대 체력.
        /// </summary>
        public float Max { get; }

        public CharacterHealthChangedEvent(GameObject character, float current, float max)
        {
            Character = character;
            Current = current;
            Max = max;
        }
    }
}
