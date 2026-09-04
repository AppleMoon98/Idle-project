using System;
using Core;
using Story;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 만화 형식 스토리(Story.StorySO)를 한 컷씩 탭으로 넘겨보는 범용 팝업. 인트로 스토리와 랭크
    /// 승급 스토리가 이 하나의 인스턴스를 공유한다(ConfirmationPopupUI가 여러 액션을 한 팝업으로
    /// 처리하는 것과 같은 관례). Play(story, onComplete)가 유일한 공개 진입점.
    /// </summary>
    public sealed class StoryPopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button panelButton;

        [SerializeField]
        private Image cutImage;

        [SerializeField]
        private Text cutText;

        [SerializeField]
        private Button skipButton;

        private StorySO _story;
        private int _cutIndex;
        private Action _onComplete;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            panelButton.onClick.AddListener(AdvanceCut);
            skipButton.onClick.AddListener(Complete);
        }

        /// <summary>
        /// story의 컷을 처음부터 탭으로 넘겨 보여준다. story가 null이거나 컷이 하나도 없으면
        /// 팝업을 띄우지 않고 즉시 onComplete를 호출한다 - 아직 스토리가 준비되지 않은 랭크가
        /// 기존 동작(알림 팝업 즉시 표시)을 그대로 유지하게 하는 콘텐츠 게이트.
        /// </summary>
        public void Play(StorySO story, Action onComplete)
        {
            if (story == null || story.Cuts == null || story.Cuts.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _story = story;
            _onComplete = onComplete;
            _cutIndex = 0;

            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
            ShowCut(_cutIndex);
        }

        private void ShowCut(int index)
        {
            StoryCutSO cut = _story.Cuts[index];

            if (cutImage != null)
            {
                bool hasImage = cut.CutImage != null;
                cutImage.gameObject.SetActive(hasImage);
                cutImage.sprite = cut.CutImage;
            }

            if (cutText != null)
            {
                bool hasText = !string.IsNullOrEmpty(cut.CutText);
                cutText.gameObject.SetActive(hasText);
                cutText.text = cut.CutText;
            }
        }

        private void AdvanceCut()
        {
            if (_story == null)
            {
                return;
            }

            _cutIndex++;

            if (_cutIndex >= _story.Cuts.Length)
            {
                Complete();
                return;
            }

            ShowCut(_cutIndex);
        }

        /// <summary>
        /// 스킵 버튼과 마지막 컷 이후의 탭이 공유하는 종료 경로 - 어느 쪽으로 끝나든 onComplete는
        /// 항상 정확히 한 번 호출된다(호출부는 스토리를 끝까지 봤는지 스킵했는지 구분할 필요가 없다).
        /// </summary>
        private void Complete()
        {
            if (_story == null)
            {
                return;
            }

            Action onComplete = _onComplete;
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
            _story = null;
            _onComplete = null;
            _cutIndex = 0;

            onComplete?.Invoke();
        }

        bool IDismissible.TryDismiss()
        {
            if (_story == null)
            {
                return false;
            }

            Complete();
            return true;
        }
    }
}
