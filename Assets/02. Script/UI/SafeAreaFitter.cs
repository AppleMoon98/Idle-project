using Core;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Screen.safeArea 기준으로 노치/펀치홀 카메라/곡면 모서리/제스처 영역을 피해
    /// 자신이 붙어 있는 화면 가장자리 쪽으로 RectTransform을 안쪽으로 밀어 넣는다.
    /// 앵커가 한 점(모서리/변)에 고정된 축은 anchoredPosition을, 양 끝에 걸쳐 늘어난(stretch)
    /// 축은 offsetMin/offsetMax를 보정한다. 화면 중앙 쪽 앵커(가장자리에 붙지 않은 축)는 건드리지 않는다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour, ITickable
    {
        private RectTransform _rectTransform;
        private Vector2 _baseAnchoredPosition;
        private Vector2 _baseOffsetMin;
        private Vector2 _baseOffsetMax;
        private Rect _lastAppliedSafeArea;
        private int _lastAppliedScreenWidth;
        private int _lastAppliedScreenHeight;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _baseAnchoredPosition = _rectTransform.anchoredPosition;
            _baseOffsetMin = _rectTransform.offsetMin;
            _baseOffsetMax = _rectTransform.offsetMax;
        }

        private void OnEnable()
        {
            Apply();

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea == _lastAppliedSafeArea &&
                Screen.width == _lastAppliedScreenWidth &&
                Screen.height == _lastAppliedScreenHeight)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            Canvas canvas = _rectTransform.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.scaleFactor <= 0f)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            float scaleFactor = canvas.scaleFactor;

            float insetLeft = safeArea.xMin / scaleFactor;
            float insetRight = (Screen.width - safeArea.xMax) / scaleFactor;
            float insetBottom = safeArea.yMin / scaleFactor;
            float insetTop = (Screen.height - safeArea.yMax) / scaleFactor;

            Vector2 anchoredPosition = _baseAnchoredPosition;
            Vector2 offsetMin = _baseOffsetMin;
            Vector2 offsetMax = _baseOffsetMax;

            ApplyHorizontalAxis(insetLeft, insetRight, ref anchoredPosition, ref offsetMin, ref offsetMax);
            ApplyVerticalAxis(insetBottom, insetTop, ref anchoredPosition, ref offsetMin, ref offsetMax);

            _rectTransform.anchoredPosition = anchoredPosition;
            _rectTransform.offsetMin = offsetMin;
            _rectTransform.offsetMax = offsetMax;

            _lastAppliedSafeArea = safeArea;
            _lastAppliedScreenWidth = Screen.width;
            _lastAppliedScreenHeight = Screen.height;
        }

        private void ApplyHorizontalAxis(float insetMin, float insetMax, ref Vector2 anchoredPosition, ref Vector2 offsetMin, ref Vector2 offsetMax)
        {
            float anchorMin = _rectTransform.anchorMin.x;
            float anchorMax = _rectTransform.anchorMax.x;

            if (!Mathf.Approximately(anchorMin, anchorMax))
            {
                offsetMin.x += insetMin;
                offsetMax.x -= insetMax;
                return;
            }

            if (Mathf.Approximately(anchorMin, 0f))
            {
                anchoredPosition.x += insetMin;
            }
            else if (Mathf.Approximately(anchorMin, 1f))
            {
                anchoredPosition.x -= insetMax;
            }
        }

        private void ApplyVerticalAxis(float insetMin, float insetMax, ref Vector2 anchoredPosition, ref Vector2 offsetMin, ref Vector2 offsetMax)
        {
            float anchorMin = _rectTransform.anchorMin.y;
            float anchorMax = _rectTransform.anchorMax.y;

            if (!Mathf.Approximately(anchorMin, anchorMax))
            {
                offsetMin.y += insetMin;
                offsetMax.y -= insetMax;
                return;
            }

            if (Mathf.Approximately(anchorMin, 0f))
            {
                anchoredPosition.y += insetMin;
            }
            else if (Mathf.Approximately(anchorMin, 1f))
            {
                anchoredPosition.y -= insetMax;
            }
        }
    }
}
