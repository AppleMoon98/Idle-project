using UnityEngine;

namespace Character.Events
{
    /// <summary>
    /// 캐릭터가 사망했을 때 EventBus를 통해 발행되는 이벤트.
    /// </summary>
    public readonly struct CharacterDiedEvent
    {
        /// <summary>
        /// 사망한 캐릭터.
        /// </summary>
        public GameObject Character { get; }

        public CharacterDiedEvent(GameObject character)
        {
            Character = character;
        }
    }
}
