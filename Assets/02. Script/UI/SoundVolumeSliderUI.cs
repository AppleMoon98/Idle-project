using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 0~100% 볼륨 슬라이더 하나를 관리한다. 배경음/효과음 슬라이더가 이 컴포넌트를 공유해서
    /// 쓰고, playerPrefsKey로 어느 값을 저장하는지만 구분한다. 아직 실제 오디오 시스템이 없어
    /// PlayerPrefs에 값만 저장/표시하며, 나중에 오디오 시스템이 생기면 이 값을 그대로 읽어 쓸 수 있다.
    /// </summary>
    public sealed class SoundVolumeSliderUI : MonoBehaviour
    {
        [SerializeField]
        private string playerPrefsKey;

        [SerializeField]
        private Slider slider;

        [SerializeField]
        private Text valueText;

        [SerializeField]
        [Range(0, 100)]
        private int defaultValue = 100;

        private void Awake()
        {
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;

            int savedValue = PlayerPrefs.GetInt(playerPrefsKey, defaultValue);
            slider.value = savedValue;
            UpdateValueText(savedValue);

            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnSliderChanged(float value)
        {
            int intValue = Mathf.RoundToInt(value);
            PlayerPrefs.SetInt(playerPrefsKey, intValue);
            PlayerPrefs.Save();
            UpdateValueText(intValue);
        }

        private void UpdateValueText(int value)
        {
            valueText.text = $"{value}%";
        }
    }
}
