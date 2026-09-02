using Core;
using Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 게임 데이터 초기화를 재차 확인받는 팝업. "예"를 누르면 진행 데이터(SaveService가 소유한
    /// Save.* 키)만 지우고 현재 씬을 다시 로드해 모든 서비스가 처음 상태로 재초기화되도록 한다.
    ///
    /// GitHub 이슈 #56 - 예전엔 PlayerPrefs.DeleteAll()을 직접 호출해 사운드/카메라 흔들림/확인창
    /// "다시 보지 않기" 같은, SaveService와 전혀 무관한 로컬 선호 설정까지 함께 지워버렸다. 이제는
    /// SaveService.ResetProgress()(그 클래스 자신이 소유한 키 목록만 아는 유일한 곳)에 위임한다 -
    /// 이 팝업은 "무엇이 진행 데이터인지" 전혀 몰라도 된다.
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
        private SaveService _saveService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
                GameBootstrapper.Services.TryGet(out _saveService);
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
            _saveService?.ResetProgress();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
