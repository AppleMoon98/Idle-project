using UnityEngine;

namespace UI.Events
{
    /// <summary>
    /// 화면이 대각선/세로 등으로 베이는 슬래시 연출을 요청하는 이벤트. 어떤 도메인이든(보스 패턴,
    /// 강한 스킬 시전 등) 이 이벤트만 발행하면 되고, 실제로 화면에 그리는 책임은 전역으로 하나만
    /// 존재하는 UI.ScreenSlashEffectUI가 진다 — Skill.Events.SkillCameraShakeRequestedEvent를
    /// Services.CameraShakeService가 구독하는 것과 같은 방향.
    /// </summary>
    public readonly struct ScreenSlashRequestedEvent
    {
        /// <summary>
        /// 슬래시 라인의 회전각(도). 0 = 수평, 90 = 수직. 생략하면 ScreenSlashEffectUI에 씬에서
        /// 미리 authored된 기본 각도를 그대로 쓴다.
        /// </summary>
        public float? AngleDegrees { get; }

        /// <summary>
        /// 슬래시가 그어질 실제 월드 좌표(예: 보스 패턴이 판정한 지점). 생략하면 화면 중앙에서
        /// 재생한다. AngleDegrees도 함께 지정돼 있어야 실제로 사용된다.
        /// </summary>
        public Vector3? WorldPosition { get; }

        public ScreenSlashRequestedEvent(float? angleDegrees = null, Vector3? worldPosition = null)
        {
            AngleDegrees = angleDegrees;
            WorldPosition = worldPosition;
        }
    }
}
