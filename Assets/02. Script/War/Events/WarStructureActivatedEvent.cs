using UnityEngine;

namespace War.Events
{
    /// <summary>
    /// 구조물이 근처 아군을 감지해 몬스터를 밀어냈을 때 발행되는 이벤트.
    /// 승패 판정에는 관여하지 않고 연출(VFX/카메라 흔들림 등)이 구독하기 위한 용도다.
    /// </summary>
    public readonly struct WarStructureActivatedEvent
    {
        /// <summary>
        /// 작동한 구조물의 위치.
        /// </summary>
        public Vector2 Position { get; }

        public WarStructureActivatedEvent(Vector2 position)
        {
            Position = position;
        }
    }
}
