using UnityEngine;

namespace Character.Events
{
    /// <summary>
    /// 집결 깃발을 드롭해 발행되는 이동 명령 이벤트. 플레이어/병사 모두 자동·수동 모드와
    /// 무관하게 이 위치로 이동한다.
    /// </summary>
    public readonly struct SquadMoveCommandEvent
    {
        /// <summary>
        /// 이동 목표 월드 좌표.
        /// </summary>
        public Vector3 WorldPosition { get; }

        public SquadMoveCommandEvent(Vector3 worldPosition)
        {
            WorldPosition = worldPosition;
        }
    }
}
