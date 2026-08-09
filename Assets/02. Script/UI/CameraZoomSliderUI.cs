using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 중단 좌측에 상시 노출되는 세로 슬라이더로 카메라 시야(orthographicSize)를 직접
    /// 조절한다. 이 슬라이더 하나만 Camera.main을 건드리는 단일 소비자 기능이라
    /// Services.CameraShakeService처럼 IManager/IService+이벤트로 만들지 않고,
    /// SoundVolumeSliderUI와 같은 "슬라이더가 값을 직접 반영하고 PlayerPrefs에 즉시 저장"
    /// 방향을 따른다. Slider.direction=BottomToTop이므로 slider.value가 0(맨 아래, "-")이면
    /// wideOrthographicSize(넓은 시야), 1(맨 위, "+")이면 narrowOrthographicSize(좁은 시야)로
    /// 선형 보간한다.
    /// </summary>
    public sealed class CameraZoomSliderUI : MonoBehaviour
    {
        private const string PlayerPrefsKey = "CameraZoomSliderValue";

        [SerializeField]
        private Slider zoomSlider;

        [SerializeField]
        private float wideOrthographicSize = 16f;

        [SerializeField]
        private float narrowOrthographicSize = 6f;

        /// <summary>
        /// 슬라이더가 도달할 수 있는 가장 좁은(확대된) orthographicSize.
        /// Services.CameraFollowService가 카메라 추적 시작 임계값으로 그대로 재사용한다 —
        /// 같은 값을 두 곳에 따로 두면 나중에 어긋나므로 여기서만 정의한다.
        /// </summary>
        public float NarrowOrthographicSize => narrowOrthographicSize;

        /// <summary>
        /// 슬라이더 value=0(맨 아래)일 때의 orthographicSize. Services.CameraFollowService가
        /// "카메라/플레이어가 벗어날 수 없는 영역"의 경계로 그대로 재사용한다 — 마찬가지로 값을
        /// 두 곳에 따로 두지 않기 위해 여기서만 정의한다.
        /// </summary>
        public float WideOrthographicSize => wideOrthographicSize;

        private Camera _camera;

        private void OnEnable()
        {
            _camera = Camera.main;

            float defaultValue = Mathf.InverseLerp(wideOrthographicSize, narrowOrthographicSize, _camera != null ? _camera.orthographicSize : wideOrthographicSize);
            float savedValue = PlayerPrefs.GetFloat(PlayerPrefsKey, defaultValue);

            zoomSlider.SetValueWithoutNotify(savedValue);
            ApplyZoom(savedValue);

            zoomSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        private void OnDisable()
        {
            zoomSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(float value)
        {
            ApplyZoom(value);
            PlayerPrefs.SetFloat(PlayerPrefsKey, value);
        }

        private void ApplyZoom(float sliderValue)
        {
            if (_camera == null)
            {
                return;
            }

            _camera.orthographicSize = Mathf.Lerp(wideOrthographicSize, narrowOrthographicSize, sliderValue);
        }
    }
}
