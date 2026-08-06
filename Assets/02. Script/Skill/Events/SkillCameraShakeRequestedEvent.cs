namespace Skill.Events
{
    /// <summary>
    /// 스킬 시전 시 카메라 흔들림 연출을 요청하는 이벤트. Skill 도메인은 Camera/Services의 존재를
    /// 전혀 모르고 이 이벤트만 발행한다 — Services.CameraShakeService가 구독해 실제 연출을 담당한다.
    /// </summary>
    public readonly struct SkillCameraShakeRequestedEvent
    {
        /// <summary>
        /// 흔들림 지속시간(초).
        /// </summary>
        public float Duration { get; }

        /// <summary>
        /// 흔들림 강도.
        /// </summary>
        public float Magnitude { get; }

        public SkillCameraShakeRequestedEvent(float duration, float magnitude)
        {
            Duration = duration;
            Magnitude = magnitude;
        }
    }
}
