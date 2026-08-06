using Services;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 흔들림 on/off 토글. SoundMuteIconToggleUI와 동일한 형태(PlayerPrefs에 저장하고
    /// 아이콘 색상으로 상태를 표시)지만, 저장 키를 인스펙터 문자열이 아니라
    /// Services.CameraShakeService.DisabledPlayerPrefsKey로 직접 참조한다 — 흔들림을 실제로
    /// 소비하는 쪽이 이미 있어서(사운드와 달리) 키 문자열이 두 곳에서 따로따로 어긋나지 않게 하기 위함이다.
    /// </summary>
    public sealed class CameraShakeToggleUI : MonoBehaviour
    {
        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Color onColor = new Color(0.55f, 0.85f, 0.95f, 1f);

        [SerializeField]
        private Color offColor = new Color(0.55f, 0.16f, 0.14f, 1f);

        private bool _isDisabled;

        private void Awake()
        {
            _isDisabled = PlayerPrefs.GetInt(CameraShakeService.DisabledPlayerPrefsKey, 0) != 0;
            toggleButton.onClick.AddListener(OnToggleClicked);
            Refresh();
        }

        private void OnToggleClicked()
        {
            _isDisabled = !_isDisabled;
            PlayerPrefs.SetInt(CameraShakeService.DisabledPlayerPrefsKey, _isDisabled ? 1 : 0);
            PlayerPrefs.Save();
            Refresh();
        }

        private void Refresh()
        {
            iconImage.color = _isDisabled ? offColor : onColor;
        }
    }
}
