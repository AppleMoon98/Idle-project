using Core;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Services.CameraFollowService.ApplyAspectPillarbox()가 16:9보다 넓적한 화면에서 카메라
    /// Rect를 좌우로 좁혀 만드는 검정 여백을, Screen Space - Overlay Canvas(카메라 Rect와 무관하게
    /// 항상 전체 화면을 쓰므로 그 자체로는 영향을 받지 않음)에도 동일하게 반영한다. 여백 폭을
    /// 새로 계산하지 않고 Camera.main.rect(CameraFollowService가 이미 계산해 적용해둔 값)를
    /// 그대로 읽어 단일 진실 공급원을 유지한다 - 같은 공식이 두 곳에 따로 있으면 나중에
    /// 어긋날 위험이 있다. 좌우 대칭이 항상 보장된다는 ApplyAspectPillarbox의 계약을 그대로
    /// 신뢰해, rect.x 하나만으로 양쪽 바의 폭을 함께 정한다.
    /// </summary>
    public sealed class ScreenPillarboxUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private RectTransform leftBar;

        [SerializeField]
        private RectTransform rightBar;

        private Canvas _canvas;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            Apply();
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight)
            {
                return;
            }

            Apply();
        }

        /// <summary>
        /// Screen.width/height가 바뀌었을 때만(SafeAreaFitter와 동일한 더티 체크) 실제로
        /// 다시 계산한다 - 화면 회전/리사이즈 시 자동으로 반영된다.
        /// </summary>
        private void Apply()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            Camera camera = Camera.main;

            if (camera == null || _canvas == null || _canvas.scaleFactor <= 0f)
            {
                return;
            }

            float marginUnits = camera.rect.x * Screen.width / _canvas.scaleFactor;

            SetBarWidth(leftBar, marginUnits);
            SetBarWidth(rightBar, marginUnits);
        }

        private static void SetBarWidth(RectTransform bar, float width)
        {
            if (bar == null)
            {
                return;
            }

            Vector2 sizeDelta = bar.sizeDelta;
            sizeDelta.x = width;
            bar.sizeDelta = sizeDelta;
        }
    }
}
