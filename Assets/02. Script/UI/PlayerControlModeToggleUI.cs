using Character;
using Character.Events;
using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 어디서나 접근 가능한 자동/수동 조작 토글 버튼. PlayerControlModeService/
    /// PlayerControlModeChangedEvent로만 상태를 주고받으며 Character 도메인 컴포넌트를 직접
    /// 참조하지 않는다.
    /// </summary>
    public sealed class PlayerControlModeToggleUI : MonoBehaviour
    {
        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Text modeLabelText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
            toggleButton.onClick.AddListener(OnToggleClicked);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService))
            {
                Refresh(controlModeService.CurrentMode);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
            toggleButton.onClick.RemoveListener(OnToggleClicked);
        }

        private void OnToggleClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService))
            {
                controlModeService.Toggle();
            }
        }

        private void OnControlModeChanged(PlayerControlModeChangedEvent evt)
        {
            Refresh(evt.Mode);
        }

        private void Refresh(PlayerControlMode mode)
        {
            modeLabelText.text = mode == PlayerControlMode.Manual ? "수동" : "자동";
        }
    }
}
