using Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 상태를 되돌리기 번거로운 액션(강화/합성 등) 실행 직전에 확인을 받는 범용 모달 팝업.
    /// 액션 종류(actionKey)별로 독립된 "다시 보지 않기" 설정을 PlayerPrefs에 저장한다 - 순수
    /// 클라이언트 선호도라 서비스/이벤트 왕복 없이 직접 읽고 쓰는 CameraShakeToggleUI/
    /// SoundVolumeSliderUI와 같은 관례를 따른다.
    /// </summary>
    public sealed class ConfirmationPopupUI : MonoBehaviour, IDismissible
    {
        /// <summary>
        /// "다시 보지 않기" PlayerPrefs 키의 접두사. NotificationSettingsPopupUI가 팝업을 다시
        /// 띄우지 않고도 이 값을 직접 읽고 써야 해서(설정 화면에서 알림을 다시 켜는 유일한
        /// 경로), 두 스크립트가 리터럴을 각자 들고 있다 어긋나지 않도록 공개 상수로 공유한다
        /// (CameraShakeService.DisabledPlayerPrefsKey, section BA와 같은 관례).
        /// </summary>
        public const string DontShowKeyPrefix = "ConfirmationPopup_DontShow_";

        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private Toggle dontShowAgainToggle;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button cancelButton;

        private Action _pendingConfirm;
        private string _pendingDontShowKey;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// actionKey에 해당하는 "다시 보지 않기"가 이미 켜져 있으면 팝업 없이 즉시 onConfirm을
        /// 실행하고, 아니면 message를 보여주는 확인 팝업을 띄운다. actionKey는 PlayerPrefs 키의
        /// 일부로 쓰이므로 호출부 간에 고유해야 한다(예: "EnhanceAll", "Fuse").
        /// </summary>
        public void RequestConfirm(string actionKey, string message, Action onConfirm)
        {
            string key = DontShowKeyPrefix + actionKey;

            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                onConfirm?.Invoke();
                return;
            }

            _pendingConfirm = onConfirm;
            _pendingDontShowKey = key;
            messageText.text = message;
            dontShowAgainToggle.isOn = false;
            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

        private void OnConfirmClicked()
        {
            if (dontShowAgainToggle.isOn && _pendingDontShowKey != null)
            {
                PlayerPrefs.SetInt(_pendingDontShowKey, 1);
            }

            Action confirm = _pendingConfirm;
            Close();
            confirm?.Invoke();
        }

        private void Close()
        {
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
            _pendingConfirm = null;
            _pendingDontShowKey = null;
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
