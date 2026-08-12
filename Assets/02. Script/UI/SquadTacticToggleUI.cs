using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업(SquadDetailPopup)의 전술 선택 버튼 — 지금 보고 있는 부대의 전술을 버튼
    /// 하나로 순환(없음 → 방패벽 → 없음 ...) 전환한다. 선택지가 두 가지뿐이라 버튼 하나로
    /// 충분하고, 전술이 늘어나면(계획된 특수 기동형 전술들) 그때 목록형 피커 UI로 승격한다.
    /// </summary>
    public sealed class SquadTacticToggleUI : MonoBehaviour
    {
        [SerializeField]
        private Button tacticButton;

        [SerializeField]
        private Text tacticLabel;

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

            SquadTacticType next = SquadTacticService.GetNext(tactics.GetTactic(_squadIndex));
            tactics.SetTactic(_squadIndex, next);
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

            tacticLabel.text = "전술: " + DisplayName(tactics.GetTactic(_squadIndex));
        }

        private static string DisplayName(SquadTacticType tactic)
        {
            return tactic switch
            {
                SquadTacticType.ShieldWall => "방패벽",
                _ => "없음",
            };
        }
    }
}
