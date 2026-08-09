using Core;
using Skill.Events;
using UnityEngine;

namespace Services
{
    /// <summary>
    /// 스킬 시전 등에서 요청하는 카메라 흔들림 연출을 담당한다. Skill 도메인은 이 서비스의 존재를
    /// 몰라도 되도록 SkillCameraShakeRequestedEvent만 구독한다(Character.StatEnhancementReceiver가
    /// Enhancement의 이벤트를 구독하는 것과 같은 방향). 카메라 위치(transform.localPosition)는 더
    /// 이상 여기서 직접 쓰지 않는다 — CameraFollowService가 카메라 위치를 쓰는 유일한 지점이고,
    /// 매 틱 CurrentOffset을 읽어 자신이 계산한 목표 위치에 더한다. 흔들림/추적 두 시스템의
    /// GameTicker 등록 순서에 관계없이 항상 같은 프레임에 정확히 합성되도록 하기 위한 구조다.
    /// </summary>
    public sealed class CameraShakeService : IManager, IService, ITickable
    {
        /// <summary>
        /// 화면 흔들림을 껐는지 저장하는 PlayerPrefs 키. UI.CameraShakeToggleUI가 이 값을
        /// 그대로 읽고 쓴다 — 두 파일에 문자열을 따로 적어 어긋나는 일이 없도록 여기서만 정의한다.
        /// </summary>
        public const string DisabledPlayerPrefsKey = "ScreenShakeDisabled";

        private readonly EventBus _events;
        private float _remaining;
        private float _magnitude;

        /// <summary>
        /// 이번 프레임 카메라가 흔들림으로 더해야 할 오프셋. 흔들림이 없으면 Vector3.zero.
        /// </summary>
        public Vector3 CurrentOffset { get; private set; }

        public CameraShakeService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events.Subscribe<SkillCameraShakeRequestedEvent>(OnShakeRequested);
            TickerRegistration.Register(this);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<SkillCameraShakeRequestedEvent>(OnShakeRequested);
            TickerRegistration.Unregister(this);
        }

        private void OnShakeRequested(SkillCameraShakeRequestedEvent evt)
        {
            if (PlayerPrefs.GetInt(DisabledPlayerPrefsKey, 0) != 0)
            {
                return;
            }

            // 재시전으로 흔들림이 겹치면 새 요청으로 갱신할 뿐 누적하지 않는다(SelfBuffSkillEffect가
            // 이전 보너스를 제거하고 새로 적용하는 것과 같은 사상) — 과도하게 흔들리는 것을 막는다.
            _remaining = evt.Duration;
            _magnitude = evt.Magnitude;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_remaining <= 0f)
            {
                CurrentOffset = Vector3.zero;
                return;
            }

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                CurrentOffset = Vector3.zero;
                return;
            }

            Vector2 offset = Random.insideUnitCircle * _magnitude;
            CurrentOffset = new Vector3(offset.x, offset.y, 0f);
        }
    }
}
