using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업의 전술 선택 버튼 — 탭하면 SquadTacticOptionPopupUI가 전체 전술 목록을
    /// 드롭다운처럼 띄운다. 배치가 더 이상 부대(1~6)를 개별 선택하지 않는 단일 풀 방식으로
    /// 바뀌면서(부대 선택 UI 자체가 삭제됨), 전술도 부대별이 아니라 하나만 골라 6개 백엔드
    /// 부대 전체(Soldier.SquadTacticService.SetTacticForAll)에 동일하게 적용한다 — 대표로
    /// 부대 0의 값을 "현재 전술"로 읽고 보여준다(SetTacticForAll이 항상 전 부대를 동기화해서
    /// 바꾸므로 0번이 나머지와 어긋날 일이 없다).
    /// </summary>
    public sealed class SquadTacticDropdownUI : MonoBehaviour
    {
        private const int RepresentativeSquadIndex = 0;

        [SerializeField]
        private Button tacticButton;

        [SerializeField]
        private Text tacticLabel;

        [SerializeField]
        private SquadTacticOptionPopupUI optionPopup;

        private void Awake()
        {
            tacticButton.onClick.AddListener(OnTacticButtonClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SquadTacticChangedEvent>(OnTacticChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SquadTacticChangedEvent>(OnTacticChanged);
        }

        private void OnTacticChanged(SquadTacticChangedEvent evt)
        {
            Refresh();
        }

        private void OnTacticButtonClicked()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SquadTacticService tactics))
            {
                return;
            }

            optionPopup.Open(tactics.GetTactic(RepresentativeSquadIndex));
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

            tacticLabel.text = "전술: " + SquadTacticDisplayNames.Get(tactics.GetTactic(RepresentativeSquadIndex));
        }
    }
}
