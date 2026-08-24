using Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UI
{
    /// <summary>
    /// 카메라 시야(orthographicSize)를 두 손가락 핀치 제스처 또는(PC 테스트용) 마우스 휠로 직접
    /// 조절한다 - 화면 왼쪽 드로어 + 토글 버튼으로 여닫던 슬라이더 UI(UI.CameraZoomSliderUI/
    /// CameraZoomDrawerUI, 삭제됨)를 완전히 대체한다. 손가락을 벌리거나 휠을 위로 굴리면 확대
    /// (orthographicSize가 작아짐), 오므리거나 휠을 아래로 굴리면 축소(커짐). 핀치는 화면
    /// 세로길이 전체만큼 벌리는 동작이 narrowOrthographicSize~wideOrthographicSize 전체 범위를
    /// 커버하도록, 민감도를 고정 배율 대신 Screen.height 기준으로 매 프레임 동적으로 계산한다 -
    /// 해상도가 달라도 항상 같은 손가락 이동 비율이 같은 줌 비율이 되도록 하기 위함. 마우스 휠은
    /// 실제 기기별 회전 단위가 표준화돼 있지 않아(모바일 타겟이라 정밀 튜닝 대상도 아님) 별도의
    /// mouseScrollSensitivity 배율을 곱한다 - 이 프로젝트는 모바일 타겟이지만 PC 환경에서도
    /// 테스트하는 경우가 있어 추가된, 개발 편의용 보조 입력이다.
    ///
    /// wideOrthographicSize/narrowOrthographicSize는 예전 슬라이더와 똑같은 역할을 그대로 이어받는다
    /// - Services.CameraFollowService(카메라 추적 시작 임계값이자 플레이어 이동 제한 경계로 재사용)와
    /// Rank.Boss.PromotionBossController(승급전 보스 페이즈2 동안 강제 최광각 전환)가 그대로
    /// 참조한다. Camera 컴포넌트에 직접 부착돼(RequireComponent) Camera.main 탐색 없이 자기 자신을
    /// 바로 조작한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraPinchZoomUI : MonoBehaviour, ITickable
    {
        /// <summary>
        /// 예전 CameraZoomSliderUI가 쓰던 PlayerPrefs 키를 그대로 재사용한다 - 저장 형식(0~1 정규화
        /// 값, wideOrthographicSize~narrowOrthographicSize 보간 비율)이 동일해 기존 저장값을 그대로
        /// 이어받는다.
        /// </summary>
        private const string PlayerPrefsKey = "CameraZoomSliderValue";

        [SerializeField]
        private float wideOrthographicSize = 24f;

        [SerializeField]
        private float narrowOrthographicSize = 6f;

        /// <summary>
        /// 마우스 휠 스크롤 한 단위(Mouse.scroll.y 원시값, 보통 노치 하나당 ±120 근방)당
        /// orthographicSize를 얼마나 바꿀지 - 기기/드라이버별 원시값 스케일이 표준화돼 있지 않은
        /// PC 보조 입력용 플레이스홀더 값(나중 튜닝 과제).
        /// </summary>
        [SerializeField]
        private float mouseScrollSensitivity = 0.02f;

        /// <summary>
        /// 가장 좁은(확대된) orthographicSize. Services.CameraFollowService가 카메라 추적 시작
        /// 임계값으로 재사용한다.
        /// </summary>
        public float NarrowOrthographicSize => narrowOrthographicSize;

        /// <summary>
        /// 가장 넓은(축소된) orthographicSize. Services.CameraFollowService가 "카메라/플레이어가
        /// 벗어날 수 없는 영역"의 경계로 재사용한다.
        /// </summary>
        public float WideOrthographicSize => wideOrthographicSize;

        private Camera _camera;
        private bool _isPinching;
        private float _previousDistance;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            float defaultValue = Mathf.InverseLerp(wideOrthographicSize, narrowOrthographicSize, _camera.orthographicSize);
            float savedValue = PlayerPrefs.GetFloat(PlayerPrefsKey, defaultValue);
            _camera.orthographicSize = Mathf.Lerp(wideOrthographicSize, narrowOrthographicSize, savedValue);

            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            TickPinch();
            TickMouseScroll();
        }

        private void TickPinch()
        {
            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen == null)
            {
                _isPinching = false;
                return;
            }

            TouchControl first = null;
            TouchControl second = null;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                if (first == null)
                {
                    first = touch;
                }
                else
                {
                    second = touch;
                    break;
                }
            }

            if (first == null || second == null)
            {
                _isPinching = false;
                return;
            }

            float currentDistance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());

            if (_isPinching)
            {
                float sizeRange = wideOrthographicSize - narrowOrthographicSize;
                float orthographicSizeDelta = -(currentDistance - _previousDistance) * sizeRange / Mathf.Max(1f, Screen.height);
                ApplyOrthographicSizeDelta(orthographicSizeDelta);
            }

            _previousDistance = currentDistance;
            _isPinching = true;
        }

        /// <summary>
        /// 모바일 타겟이지만 PC에서도 테스트하는 경우가 있어 추가된 보조 입력 - 휠을 위로 굴리면
        /// (양수 값) 확대, 아래로 굴리면(음수 값) 축소. 터치스크린이 없는 개발 환경에서 핀치를
        /// 대신할 수 있도록 한다.
        /// </summary>
        private void TickMouseScroll()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            float scrollY = mouse.scroll.ReadValue().y;

            if (Mathf.Approximately(scrollY, 0f))
            {
                return;
            }

            ApplyOrthographicSizeDelta(-scrollY * mouseScrollSensitivity);
        }

        private void ApplyOrthographicSizeDelta(float orthographicSizeDelta)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + orthographicSizeDelta, narrowOrthographicSize, wideOrthographicSize);

            float normalizedValue = Mathf.InverseLerp(wideOrthographicSize, narrowOrthographicSize, _camera.orthographicSize);
            PlayerPrefs.SetFloat(PlayerPrefsKey, normalizedValue);
        }
    }
}
