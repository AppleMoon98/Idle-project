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
    /// 원시값 크기가 기기/드라이버마다 표준화돼 있지 않아(핀치처럼 원시값에 비례시키면 노치 하나로
    /// 체감 불가능한 변화만 날 수 있다) 방향만 읽어 노치 하나당 mouseScrollStepSize만큼의 고정
    /// 스텝으로 움직인다 - 이 프로젝트는 모바일 타겟이지만 PC 환경에서도 테스트하는 경우가 있어
    /// 추가된, 개발 편의용 보조 입력이다.
    ///
    /// wideOrthographicSize/narrowOrthographicSize는 예전 슬라이더와 똑같은 역할을 그대로 이어받는다
    /// - Services.CameraFollowService(카메라 추적 시작 임계값이자 플레이어 이동 제한 경계로 재사용)와
    /// Rank.Boss.PromotionBossController(승급전 보스 페이즈2 동안 강제 최광각 전환)가 그대로
    /// 참조한다. Camera 컴포넌트에 직접 부착돼(RequireComponent) Camera.main 탐색 없이 자기 자신을
    /// 바로 조작한다.
    ///
    /// 마우스/터치 둘 다 매 틱 UI.PointerOverUI.IsOverUI로 포인터가 UI(스크롤 가능한 팝업 등) 위에
    /// 있는지 먼저 확인하고, 위에 있으면 이 프레임의 카메라 입력을 건너뛴다 - 예전엔 이 확인이
    /// 전혀 없어 팝업을 스크롤하는 마우스 휠/핀치가 배경 전투 카메라 줌까지 함께 바꿨다(GitHub
    /// 이슈 #11). 핀치는 두 터치 중 하나라도 UI 위면 전체 제스처를 건너뛴다.
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
        /// 휠 노치 한 번당 orthographicSize를 얼마나 바꿀지(고정 스텝). Mouse.scroll.y의 원시값
        /// 크기는 기기/드라이버마다 표준화돼 있지 않다 - 실측 결과 이 환경에서는 노치 하나당 겨우
        /// ±1 근방만 들어와, 원시값에 비례한 배율(과거 0.02)로는 노치 하나로는 체감 불가능한 변화
        /// (18 범위 중 0.02)만 나서 "휠을 굴려도 줌이 안 움직인다"는 것으로 보고됐었다 - 그래서
        /// 원시값 크기를 아예 무시하고 방향(Mathf.Sign)만 읽어 매 노치마다 이 고정 크기만큼
        /// 움직이도록 바꿨다. 휠은 핀치와 달리 연속적인 제스처가 아니라 이산적인 "노치" 입력이라
        /// 이 방식이 더 자연스럽다.
        /// </summary>
        [SerializeField]
        private float mouseScrollStepSize = 1.5f;

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

            Vector2 firstPosition = first.position.ReadValue();
            Vector2 secondPosition = second.position.ReadValue();

            // 두 터치 중 하나라도 UI(팝업 스크롤뷰 등) 위에 있으면 이 프레임은 카메라 줌으로
            // 넘기지 않는다(GitHub 이슈 #11) - _isPinching을 꺼서 델타 계산 자체를 건너뛰고,
            // _previousDistance도 갱신하지 않는다 - 그래야 손가락이 UI 밖으로 벗어나 다시 잡히는
            // 첫 프레임이 "새로 시작하는 제스처"(거리만 기록, 델타 없음)로 처리돼 그 사이 누적된
            // 거리 변화가 한꺼번에 점프하듯 적용되는 걸 막는다.
            if (PointerOverUI.IsOverUI(firstPosition) || PointerOverUI.IsOverUI(secondPosition))
            {
                _isPinching = false;
                return;
            }

            float currentDistance = Vector2.Distance(firstPosition, secondPosition);

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
        /// 대신할 수 있도록 한다. 포인터가 UI(스크롤 가능한 팝업 등) 위에 있으면 휠 입력을 그
        /// UI에 맡기고 카메라 줌은 건드리지 않는다(GitHub 이슈 #11).
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

            if (PointerOverUI.IsOverUI(mouse.position.ReadValue()))
            {
                return;
            }

            ApplyOrthographicSizeDelta(-Mathf.Sign(scrollY) * mouseScrollStepSize);
        }

        private void ApplyOrthographicSizeDelta(float orthographicSizeDelta)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + orthographicSizeDelta, narrowOrthographicSize, wideOrthographicSize);

            float normalizedValue = Mathf.InverseLerp(wideOrthographicSize, narrowOrthographicSize, _camera.orthographicSize);
            PlayerPrefs.SetFloat(PlayerPrefsKey, normalizedValue);
        }
    }
}
