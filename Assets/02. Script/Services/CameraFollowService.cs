using Core;
using UI;
using UnityEngine;

namespace Services
{
    /// <summary>
    /// UI.CameraPinchZoomUI(핀치 줌)로 orthographicSize가 wideOrthographicSize보다 조금이라도
    /// 작아져(확대돼) 있으면 카메라가 플레이어를 따라간다. orthographicSize=wideOrthographicSize일
    /// 때 보이던 사각형(카메라의 원래 고정 위치를 중심으로 한 wideOrthographicSize 기준 뷰) 밖으로는
    /// 어떤 줌 배율에서도 카메라가 나갈 수 없다 — 그 경계값을 Character.PlayerWorldBoundsConstraint가
    /// 그대로 재사용해 플레이어 자신의 이동도 같은 사각형 안으로 제한한다(HomeLocalPosition/
    /// GetWorldBoundsHalfExtent로 공개).
    /// 카메라 위치(transform.localPosition)를 쓰는 유일한 지점이며, CameraShakeService의 흔들림
    /// 오프셋을 매 틱 조회해 자신이 계산한 목표 위치에 더한다(등록 순서 의존 없이 항상 합성됨).
    /// </summary>
    public sealed class CameraFollowService : IManager, IService, ITickable
    {
        private const float FollowThresholdEpsilon = 0.0001f;

        /// <summary>
        /// 세로 뷰(2×orthographicSize)는 기기와 무관하게 항상 고정이지만, 가로 뷰는
        /// orthographicSize×Camera.aspect라 화면비가 넓은(좌우로 넓은) 기기일수록 최광각에서
        /// 더 넓은 가로 폭이 그대로 노출된다 - 배경 아트가 덮은 폭을 넘어서면 맵 바깥이,
        /// 스폰/방황 좌표(GetRandomPointWithinBounds 등)도 더 멀리서 뽑혀 화면에 그대로
        /// 보일 수 있다. 실제 안드로이드/iOS 폰 대부분은 9:16(0.5625)보다 좁아(세로로 더 김)
        /// 이 상한 안쪽이고, 이보다 넓은 기기(태블릿 등 예외 케이스)에서만 아래
        /// ApplyAspectPillarbox가 카메라 Rect를 좁혀 좌우에 여백을 준다 - Screen Space Overlay
        /// Canvas는 카메라 Rect와 무관하게 항상 전체 화면을 쓰므로 UI는 이 영향을 받지 않는다.
        /// </summary>
        private const float MaxSupportedAspect = 0.5625f;

        private readonly Transform _playerTransform;
        private readonly CameraPinchZoomUI _pinchZoom;

        private Transform _cameraTransform;
        private Camera _camera;
        private Vector3 _homeLocalPosition;
        private Vector3? _overrideTargetPosition;
        private int _lastPillarboxScreenWidth;
        private int _lastPillarboxScreenHeight;

        /// <summary>
        /// 카메라의 원래 고정 위치(경계 사각형의 중심). Camera.main이 루트(부모 없음)라는 기존
        /// 전제 하에 로컬 좌표를 그대로 월드 좌표로도 사용한다.
        /// </summary>
        public Vector3 HomeLocalPosition => _homeLocalPosition;

        public CameraFollowService(Transform playerTransform, CameraPinchZoomUI pinchZoom)
        {
            _playerTransform = playerTransform;
            _pinchZoom = pinchZoom;
        }

        public void Initialize()
        {
            _camera = Camera.main;

            if (_camera != null)
            {
                _cameraTransform = _camera.transform;
                _homeLocalPosition = _cameraTransform.localPosition;
                ApplyAspectPillarbox();
            }

            TickerRegistration.Register(this);
        }

        public void Shutdown()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// 설정하면 플레이어 추적/홈 위치 로직을 완전히 무시하고 카메라를 이 월드 좌표로 강제 고정한다.
        /// Rank.Boss.PromotionBossController의 체력 50% 페이즈처럼, 보스가 맵 중앙으로 순간이동해
        /// 플레이어와 멀리 떨어진 곳에서 연출이 진행되는 동안(기본 줌 상태에서는 카메라가 여전히
        /// 플레이어를 따라가므로 방치하면 연출 전체가 화면 밖에서 벌어진다) 카메라를 그 연출 위치에
        /// 붙잡아두는 용도. null을 넘기면 정상 추적으로 즉시 복귀한다.
        /// </summary>
        public void SetOverrideTarget(Vector3? worldPosition)
        {
            _overrideTargetPosition = worldPosition;
        }

        /// <summary>
        /// wideOrthographicSize 기준 뷰의 절반 폭/높이. Camera.main이 없거나
        /// pinchZoom이 없으면 Vector2.zero(경계 없음이 아니라 "계산 불가"를 뜻함 — 호출부에서
        /// 별도로 null 체크할 필요 없이 클램프 폭이 0이 되어 홈 위치에 고정되는 것으로 안전하게 처리됨).
        /// </summary>
        public Vector2 GetWorldBoundsHalfExtent()
        {
            if (_camera == null || _pinchZoom == null)
            {
                return Vector2.zero;
            }

            float wideSize = _pinchZoom.WideOrthographicSize;
            return new Vector2(wideSize * _camera.aspect, wideSize);
        }

        /// <summary>
        /// wideOrthographicSize 기준 고정 범위 안의 랜덤한 월드 좌표를 반환한다. margin은
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

            // z는 카메라 자신의 depth(center.z, 보통 -10)가 아니라 게임 평면인 0이어야 한다 - 카메라와
            // 같은 z에 스프라이트를 놓으면 근평면(near clip) 안쪽이라 아예 렌더링되지 않는다. 몬스터
            // 스폰처럼 스폰 직후 CharacterMover가 Player(z=0) 쪽으로 움직이는 경우는 z도 같이 빠르게
            // 보정돼 잠깐만 안 보이고 넘어가지만, Skill.Effects.MeteorSkillEffect의 예고 표시처럼 한 번
            // 배치된 뒤 전혀 움직이지 않는 정지 비주얼은 계속 카메라 z에 박혀 영원히 안 보이는 채로
            // 남는다 - 실사용 중 "포탄 낙하 범위가 안 보인다"는 리포트로 발견됨.
            return new Vector3(x, y, 0f);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_cameraTransform == null || _camera == null)
            {
                return;
            }

            if (Screen.width != _lastPillarboxScreenWidth || Screen.height != _lastPillarboxScreenHeight)
            {
                ApplyAspectPillarbox();
            }

            Vector3 target = ComputeTargetLocalPosition();

            Vector3 shakeOffset = Vector3.zero;
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraShakeService shakeService))
            {
                shakeOffset = shakeService.CurrentOffset;
            }

            _cameraTransform.localPosition = target + shakeOffset;
        }

        /// <summary>
        /// 실제 화면비가 MaxSupportedAspect보다 넓으면 카메라 Rect를 좌우로 좁혀(필러박스) 그
        /// 이상 가로로 세계가 노출되지 않게 한다 - 이후 Camera.aspect(읽기 전용, Rect에서
        /// 자동 재계산됨)를 쓰는 GetWorldBoundsHalfExtent 등 모든 계산이 이 클램프된 값을
        /// 자연히 물려받는다. 상한 이내(실제 폰 대부분)에서는 Rect를 항상 전체 화면으로 되돌려
        /// 기존과 동일한 풀스크린 동작을 유지한다.
        /// </summary>
        private void ApplyAspectPillarbox()
        {
            _lastPillarboxScreenWidth = Screen.width;
            _lastPillarboxScreenHeight = Screen.height;

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            float actualAspect = (float)Screen.width / Screen.height;

            if (actualAspect <= MaxSupportedAspect)
            {
                _camera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            float widthScale = MaxSupportedAspect / actualAspect;
            _camera.rect = new Rect((1f - widthScale) * 0.5f, 0f, widthScale, 1f);
        }

        private Vector3 ComputeTargetLocalPosition()
        {
            if (_overrideTargetPosition.HasValue)
            {
                return _overrideTargetPosition.Value;
            }

            bool shouldFollow = _playerTransform != null
                && _pinchZoom != null
                && _camera.orthographicSize < _pinchZoom.WideOrthographicSize - FollowThresholdEpsilon;

            if (!shouldFollow)
            {
                return _homeLocalPosition;
            }

            float aspect = _camera.aspect;
            float wideSize = _pinchZoom.WideOrthographicSize;
            float maxOffsetY = Mathf.Max(0f, wideSize - _camera.orthographicSize);
            float maxOffsetX = Mathf.Max(0f, (wideSize - _camera.orthographicSize) * aspect);

            float clampedX = Mathf.Clamp(_playerTransform.position.x, _homeLocalPosition.x - maxOffsetX, _homeLocalPosition.x + maxOffsetX);
            float clampedY = Mathf.Clamp(_playerTransform.position.y, _homeLocalPosition.y - maxOffsetY, _homeLocalPosition.y + maxOffsetY);

            return new Vector3(clampedX, clampedY, _homeLocalPosition.z);
        }
    }
}
