using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// ◀ 숫자 ▶ 형태의 단계 선택 스테퍼. minStage~maxStage 범위 안에서만 증감하며,
    /// 던전이나 입장 로직은 전혀 모른다 — 입장 버튼은 이 컴포넌트가 건드리지 않는다.
    /// </summary>
    public sealed class StageStepperUI : MonoBehaviour
    {
        [SerializeField]
        private Text stageNumberText;

        [SerializeField]
        private Button decreaseButton;

        [SerializeField]
        private Button increaseButton;

        [SerializeField]
        private int minStage = 1;

        [SerializeField]
        private int maxStage = 999;

        private int _currentStage;

        /// <summary>
        /// 현재 선택된 단계.
        /// </summary>
        public int CurrentStage => _currentStage;

        private void Awake()
        {
            _currentStage = minStage;
            decreaseButton.onClick.AddListener(Decrease);
            increaseButton.onClick.AddListener(Increase);
            Refresh();
        }

        private void Decrease()
        {
            _currentStage = Mathf.Max(minStage, _currentStage - 1);
            Refresh();
        }

        private void Increase()
        {
            _currentStage = Mathf.Min(maxStage, _currentStage + 1);
            Refresh();
        }

        private void Refresh()
        {
            stageNumberText.text = _currentStage.ToString();
        }
    }
}
