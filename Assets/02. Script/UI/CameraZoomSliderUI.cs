using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 카메라 시야(orthographicSize)를 직접 조절하는 슬라이더. 화면 왼쪽 드로어(zoomSlider,
    /// UI.CameraZoomDrawerUI가 여닫는 CameraZoomControl 안)와 설정 팝업(settingsSlider, 선택)
    /// 두 곳에서 같은 값을 동시에 조절할 수 있다 — 둘 중 어느 쪽을 움직여도 카메라에 적용 +
    /// PlayerPrefs 저장을 하고, 나머지 한쪽을 SetValueWithoutNotify로 맞춰 서로 무한 루프 없이
    /// 동기화한다. 이 슬라이더(들)만 Camera.main을 건드리는 단일 소비자 기능이라
    /// Services.CameraShakeService처럼 IManager/IService+이벤트로 만들지 않고,
    /// SoundVolumeSliderUI와 같은 "슬라이더가 값을 직접 반영하고 PlayerPrefs에 즉시 저장"
    /// 방향을 따른다. zoomSlider는 Slider.direction=BottomToTop이므로 slider.value가 0(맨 아래,
    /// "-")이면 wideOrthographicSize(넓은 시야), 1(맨 위, "+")이면 narrowOrthographicSize(좁은
    /// 시야)로 선형 보간한다 - settingsSlider는 가로 방향이지만 같은 0~1 값 범위를 그대로 공유한다.
    /// </summary>
    public sealed class CameraZoomSliderUI : MonoBehaviour
    {
        private const string PlayerPrefsKey = "CameraZoomSliderValue";

        [SerializeField]
        private Slider zoomSlider;

        [SerializeField]
        private Slider settingsSlider;

        [SerializeField]
        private Text settingsValueText;

        [SerializeField]
        private float wideOrthographicSize = 24f;

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
            settingsSlider?.SetValueWithoutNotify(savedValue);
            ApplyZoom(savedValue);
            UpdateSettingsValueText(savedValue);

            zoomSlider.onValueChanged.AddListener(OnZoomSliderChanged);

            if (settingsSlider != null)
            {
                settingsSlider.onValueChanged.AddListener(OnSettingsSliderChanged);
            }
        }

        private void OnDisable()
        {
            zoomSlider.onValueChanged.RemoveListener(OnZoomSliderChanged);

            if (settingsSlider != null)
            {
                settingsSlider.onValueChanged.RemoveListener(OnSettingsSliderChanged);
            }
        }

        private void OnZoomSliderChanged(float value)
        {
            settingsSlider?.SetValueWithoutNotify(value);
            HandleValueChanged(value);
        }

        private void OnSettingsSliderChanged(float value)
        {
            zoomSlider.SetValueWithoutNotify(value);
            HandleValueChanged(value);
        }

        private void HandleValueChanged(float value)
        {
            ApplyZoom(value);
            UpdateSettingsValueText(value);
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

        private void UpdateSettingsValueText(float sliderValue)
        {
            if (settingsValueText != null)
            {
                settingsValueText.text = $"{Mathf.RoundToInt(sliderValue * 100f)}%";
            }
        }
    }
}
