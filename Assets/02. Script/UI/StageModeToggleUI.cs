using Core;
using Stage;
using Stage.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 어디서나 접근 가능한 돌파/반복 스테이지 진행 방침 토글 버튼. StageModeService/
    /// StageModeChangedEvent로만 상태를 주고받으며 Stage 도메인 컴포넌트를 직접 참조하지 않는다.
    /// PlayerControlModeToggleUI와 동일한 모양(같은 문제: 플레이어가 고르는 전역 모드 표시+전환).
    /// </summary>
    public sealed class StageModeToggleUI : MonoBehaviour
    {
        [SerializeField]
        private Button toggleButton;

        [SerializeField]
        private Text modeLabelText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageModeChangedEvent>(OnStageModeChanged);
            toggleButton.onClick.AddListener(OnToggleClicked);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                Refresh(modeService.CurrentMode);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageModeChangedEvent>(OnStageModeChanged);
            toggleButton.onClick.RemoveListener(OnToggleClicked);
        }

        private void OnToggleClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                modeService.Toggle();
            }
        }

        private void OnStageModeChanged(StageModeChangedEvent evt)
        {
            Refresh(evt.Mode);
        }

        private void Refresh(StageProgressionMode mode)
        {
            modeLabelText.text = mode == StageProgressionMode.Repeat ? "반복" : "돌파";
        }
    }
}
