using Core;
using UI;
using UnityEngine;

namespace Services
{
    /// <summary>
    /// UI.CameraZoomSliderUI 슬라이더 value가 0(가장 넓은 시야, wideOrthographicSize)보다 조금이라도
    /// 확대돼 있으면 카메라가 플레이어를 따라간다. value=0일 때 보이던 사각형(카메라의 원래 고정
    /// 위치를 중심으로 한 wideOrthographicSize 기준 뷰) 밖으로는 어떤 줌 배율에서도 카메라가 나갈 수
    /// 없다 — 그 경계값을 Character.PlayerWorldBoundsConstraint가 그대로 재사용해 플레이어 자신의
    /// 이동도 같은 사각형 안으로 제한한다(HomeLocalPosition/GetWorldBoundsHalfExtent로 공개).
    /// 카메라 위치(transform.localPosition)를 쓰는 유일한 지점이며, CameraShakeService의 흔들림
    /// 오프셋을 매 틱 조회해 자신이 계산한 목표 위치에 더한다(등록 순서 의존 없이 항상 합성됨).
    /// </summary>
    public sealed class CameraFollowService : IManager, IService, ITickable
    {
        private const float FollowThresholdEpsilon = 0.0001f;

        private readonly Transform _playerTransform;
        private readonly CameraZoomSliderUI _zoomSlider;

        private Transform _cameraTransform;
        private Camera _camera;
        private Vector3 _homeLocalPosition;

        /// <summary>
        /// 카메라의 원래 고정 위치(경계 사각형의 중심). Camera.main이 루트(부모 없음)라는 기존
        /// 전제 하에 로컬 좌표를 그대로 월드 좌표로도 사용한다.
        /// </summary>
        public Vector3 HomeLocalPosition => _homeLocalPosition;

        public CameraFollowService(Transform playerTransform, CameraZoomSliderUI zoomSlider)
        {
            _playerTransform = playerTransform;
            _zoomSlider = zoomSlider;
        }

        public void Initialize()
        {
            _camera = Camera.main;

            if (_camera != null)
            {
                _cameraTransform = _camera.transform;
                _homeLocalPosition = _cameraTransform.localPosition;
            }

            TickerRegistration.Register(this);
        }

        public void Shutdown()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// value=0(wideOrthographicSize) 기준 뷰의 절반 폭/높이. Camera.main이 없거나
        /// zoomSlider가 없으면 Vector2.zero(경계 없음이 아니라 "계산 불가"를 뜻함 — 호출부에서
        /// 별도로 null 체크할 필요 없이 클램프 폭이 0이 되어 홈 위치에 고정되는 것으로 안전하게 처리됨).
        /// </summary>
        public Vector2 GetWorldBoundsHalfExtent()
        {
            if (_camera == null || _zoomSlider == null)
            {
                return Vector2.zero;
            }

            float wideSize = _zoomSlider.WideOrthographicSize;
            return new Vector2(wideSize * _camera.aspect, wideSize);
        }

        /// <summary>
        /// value=0(wideOrthographicSize) 기준 고정 범위 안의 랜덤한 월드 좌표를 반환한다. margin은
        /// 범위 가장자리에서 제외할 비율(0~0.5). 줌 배율과 무관하게 항상 같은 범위에서 뽑아야 하는
        /// 모든 곳(던전 스폰, 몬스터 방황 목적지 등)이 이 하나의 계산을 공유한다 — 실시간
        /// Camera.main 뷰포트를 직접 쓰면 플레이어가 확대할수록 범위가 좁아져 버리는 문제가
        /// 반복적으로 발생했다(Dungeon.DungeonSpawnUtility.RandomOnScreenPosition, 섹션 CG /
        /// Character.RandomWanderer가 각각 겪은 문제).
        /// </summary>
        public Vector3 GetRandomPointWithinBounds(float margin)
        {
            Vector3 center = _homeLocalPosition;
            Vector2 halfExtent = GetWorldBoundsHalfExtent();
            float marginX = halfExtent.x * 2f * margin;
            float marginY = halfExtent.y * 2f * margin;

            float x = Random.Range(center.x - halfExtent.x + marginX, center.x + halfExtent.x - marginX);
            float y = Random.Range(center.y - halfExtent.y + marginY, center.y + halfExtent.y - marginY);

            return new Vector3(x, y, center.z);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_cameraTransform == null || _camera == null)
            {
                return;
            }

            Vector3 target = ComputeTargetLocalPosition();

            Vector3 shakeOffset = Vector3.zero;
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraShakeService shakeService))
            {
                shakeOffset = shakeService.CurrentOffset;
            }

            _cameraTransform.localPosition = target + shakeOffset;
        }

        private Vector3 ComputeTargetLocalPosition()
        {
            bool shouldFollow = _playerTransform != null
                && _zoomSlider != null
                && _camera.orthographicSize < _zoomSlider.WideOrthographicSize - FollowThresholdEpsilon;

            if (!shouldFollow)
            {
                return _homeLocalPosition;
            }

            float aspect = _camera.aspect;
            float wideSize = _zoomSlider.WideOrthographicSize;
            float maxOffsetY = Mathf.Max(0f, wideSize - _camera.orthographicSize);
            float maxOffsetX = Mathf.Max(0f, (wideSize - _camera.orthographicSize) * aspect);

            float clampedX = Mathf.Clamp(_playerTransform.position.x, _homeLocalPosition.x - maxOffsetX, _homeLocalPosition.x + maxOffsetX);
            float clampedY = Mathf.Clamp(_playerTransform.position.y, _homeLocalPosition.y - maxOffsetY, _homeLocalPosition.y + maxOffsetY);

            return new Vector3(clampedX, clampedY, _homeLocalPosition.z);
        }
    }
}
