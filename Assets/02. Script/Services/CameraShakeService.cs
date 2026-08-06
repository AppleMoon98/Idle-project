using Core;
using Skill.Events;
using UnityEngine;

namespace Services
{
    /// <summary>
    /// 스킬 시전 등에서 요청하는 카메라 흔들림 연출을 담당한다. Skill 도메인은 이 서비스의 존재를
    /// 몰라도 되도록 SkillCameraShakeRequestedEvent만 구독한다(Character.StatEnhancementReceiver가
    /// Enhancement의 이벤트를 구독하는 것과 같은 방향). 카메라를 따라다니게 하는 별도 시스템이
    /// 없는 고정 카메라라는 전제로, 기준 위치를 한 번만 캐싱해 그 기준으로 흔든다 — 나중에 카메라
    /// 추적 시스템이 생기면 기준 위치를 매 틱 그쪽에서 읽어오도록 바꾸면 된다.
    /// </summary>
    public sealed class CameraShakeService : IManager, IService, ITickable
    {
        /// <summary>
        /// 화면 흔들림을 껐는지 저장하는 PlayerPrefs 키. UI.CameraShakeToggleUI가 이 값을
        /// 그대로 읽고 쓴다 — 두 파일에 문자열을 따로 적어 어긋나는 일이 없도록 여기서만 정의한다.
        /// </summary>
        public const string DisabledPlayerPrefsKey = "ScreenShakeDisabled";

        private readonly EventBus _events;
        private Transform _cameraTransform;
        private Vector3 _basePosition;
        private float _remaining;
        private float _magnitude;

        public CameraShakeService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                _cameraTransform = mainCamera.transform;
                _basePosition = _cameraTransform.localPosition;
            }

            _events.Subscribe<SkillCameraShakeRequestedEvent>(OnShakeRequested);
            TickerRegistration.Register(this);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<SkillCameraShakeRequestedEvent>(OnShakeRequested);
            TickerRegistration.Unregister(this);

            if (_cameraTransform != null)
            {
                _cameraTransform.localPosition = _basePosition;
            }
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
            if (_cameraTransform == null || _remaining <= 0f)
            {
                return;
            }

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                _cameraTransform.localPosition = _basePosition;
                return;
            }

            Vector2 offset = Random.insideUnitCircle * _magnitude;
            _cameraTransform.localPosition = _basePosition + new Vector3(offset.x, offset.y, 0f);
        }
    }
}
