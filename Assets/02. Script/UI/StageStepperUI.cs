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

        /// <summary>
        /// 최대 선택 가능 단계를 런타임에 갱신한다(예: 콘텐츠가 실제로 존재하는 챕터 수만큼만
        /// 선택 가능하도록). 이미 선택된 단계가 새 상한을 넘으면 상한으로 내려앉힌다.
        /// Awake()보다 먼저 호출되면 안 되므로(아직 minStage로 초기화되지 않았을 수 있음),
        /// 호출하는 쪽은 OnEnable 이후(Unity가 같은 활성화에서 모든 Awake를 먼저 끝낸 뒤) 호출해야 한다.
        /// </summary>
        public void SetMaxStage(int max)
        {
            maxStage = Mathf.Max(minStage, max);
            _currentStage = Mathf.Clamp(_currentStage, minStage, maxStage);
            Refresh();
        }

        private void Refresh()
        {
            stageNumberText.text = _currentStage.ToString();
        }
    }
}
