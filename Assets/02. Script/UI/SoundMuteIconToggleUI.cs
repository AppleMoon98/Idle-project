using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 배경음/효과음 한 채널의 음소거 아이콘 토글. 클릭할 때마다 on/off가 뒤집히고
    /// PlayerPrefs에 저장하며, 아이콘 색상으로 상태를 표시한다(임시 스프라이트라 아이콘
    /// 모양 대신 색상으로 구분). 아직 실제 오디오 시스템이 없어 재생을 제어하지는 않으며,
    /// 나중에 오디오 시스템이 생기면 이 저장값을 그대로 읽어 채널별 음소거에 쓸 수 있다.
    /// </summary>
    public sealed class SoundMuteIconToggleUI : MonoBehaviour
    {
        [SerializeField]
        private string playerPrefsKey;

        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Color onColor = new Color(0.55f, 0.85f, 0.95f, 1f);

        [SerializeField]
        private Color offColor = new Color(0.55f, 0.16f, 0.14f, 1f);

        private bool _isMuted;

        private void Awake()
        {
            _isMuted = PlayerPrefs.GetInt(playerPrefsKey, 0) != 0;
            toggleButton.onClick.AddListener(OnToggleClicked);
            Refresh();
        }

        private void OnToggleClicked()
        {
            _isMuted = !_isMuted;
            PlayerPrefs.SetInt(playerPrefsKey, _isMuted ? 1 : 0);
            PlayerPrefs.Save();
            Refresh();
        }

        private void Refresh()
        {
            iconImage.color = _isMuted ? offColor : onColor;
        }
    }
}
