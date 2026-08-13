using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업(SquadDetailPopup)의 전술 선택 버튼 — 탭하면 SquadTacticOptionPopupUI가
    /// 전체 전술 목록을 드롭다운처럼 띄운다(뽑기 화면의 카테고리 버튼과 같은 "탭 → 목록 팝업"
    /// 형태). 이전엔 선택지가 두 가지뿐이라 버튼 하나로 순환 토글했지만(section DL), 전술
    /// 종류가 앞으로 계속 늘어날 예정이라(section DL이 예고한 "특수 기동형" 전술들) 그때 가서
    /// 선택하기 어려워지기 전에 미리 목록형으로 승격했다. 이 컴포넌트 자체는 "지금 선택된
    /// 전술이 무엇인지 라벨로 보여주고, 탭하면 목록을 연다"는 것만 안다 — 실제 선택 처리는
    /// SquadTacticOptionPopupUI가 담당한다.
    /// </summary>
    public sealed class SquadTacticDropdownUI : MonoBehaviour
    {
        [SerializeField]
        private Button tacticButton;

        [SerializeField]
        private Text tacticLabel;

        [SerializeField]
        private SquadTacticOptionPopupUI optionPopup;

        private int _squadIndex;

        private void Awake()
        {
            tacticButton.onClick.AddListener(OnTacticButtonClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SquadTacticChangedEvent>(OnTacticChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SquadTacticChangedEvent>(OnTacticChanged);
        }

        /// <summary>
        /// 표시할 부대를 바꾸고 즉시 새로고침한다. SoldierSquadSelectorUI가 부대 버튼을 탭할 때 호출한다.
        /// </summary>
        public void ShowSquad(int squadIndex)
        {
            _squadIndex = squadIndex;
            Refresh();
        }

        private void OnTacticChanged(SquadTacticChangedEvent evt)
        {
            if (evt.SquadIndex == _squadIndex)
            {
                Refresh();
            }
        }

        private void OnTacticButtonClicked()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SquadTacticService tactics))
            {
                return;
            }

            optionPopup.Open(_squadIndex, tactics.GetTactic(_squadIndex));
        }

        private void Refresh()
        {
            if (tacticLabel == null)
            {
                return;
            }

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SquadTacticService tactics))
            {
                tacticLabel.text = "전술: -";
                return;
            }

            tacticLabel.text = "전술: " + SquadTacticDisplayNames.Get(tactics.GetTactic(_squadIndex));
        }
    }
}
