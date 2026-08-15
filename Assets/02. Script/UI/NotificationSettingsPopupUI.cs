using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// ConfirmationPopupUI가 액션별로 관리하는 "다시 보지 않기" PlayerPrefs 플래그를, 그 확인
    /// 팝업을 다시 띄우지 않고도 설정 화면에서 직접 켜고 끌 수 있게 하는 팝업. 액션 하나당
    /// 라벨+체크박스 한 줄이며, 체크 상태는 "이 알림을 표시한다"는 뜻이므로 ConfirmationPopupUI의
    /// "다시 보지 않기" 플래그와는 값이 반전된다(체크됨 = 플래그 0).
    /// </summary>
    public sealed class NotificationSettingsPopupUI : MonoBehaviour
    {
        [Serializable]
        private struct Row
        {
            public string ActionKey;
            public Toggle ShowToggle;
        }

        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Row[] rows;

        private void Awake()
        {
            popupRoot.SetActive(false);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);

            foreach (Row row in rows)
            {
                string actionKey = row.ActionKey;
                row.ShowToggle.onValueChanged.AddListener(isOn => SetShow(actionKey, isOn));
            }
        }

        private void Open()
        {
            foreach (Row row in rows)
            {
                row.ShowToggle.SetIsOnWithoutNotify(IsShowEnabled(row.ActionKey));
            }

            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }

        private static bool IsShowEnabled(string actionKey)
        {
            return PlayerPrefs.GetInt(ConfirmationPopupUI.DontShowKeyPrefix + actionKey, 0) == 0;
        }

        private static void SetShow(string actionKey, bool show)
        {
            PlayerPrefs.SetInt(ConfirmationPopupUI.DontShowKeyPrefix + actionKey, show ? 0 : 1);
        }
    }
}
