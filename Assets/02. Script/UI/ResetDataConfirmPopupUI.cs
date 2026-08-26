using Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 게임 데이터 초기화를 재차 확인받는 팝업. "예"를 누르면 PlayerPrefs를 전부 지우고
    /// 현재 씬을 다시 로드해 모든 서비스가 처음 상태로 재초기화되도록 한다.
    /// </summary>
    public sealed class ResetDataConfirmPopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button cancelButton;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            openButton.onClick.AddListener(Open);
            cancelButton.onClick.AddListener(Close);
            confirmButton.onClick.AddListener(ConfirmReset);
        }

        private void Open()
        {
            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
        }

        private void ConfirmReset()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
