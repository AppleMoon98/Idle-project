using Core;
using Rank.Events;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// "배치 관리" 팝업(SoldierDeploymentPopup)에 들어있는 부대 선택 바 —
    /// SoldierDeploymentService.SquadCount(6)개의 버튼을 인스펙터에 고정 배열로 갖는다(부대 수가
    /// 상수라 EquippedSlotBarUI처럼 코드로 동적 생성할 필요가 없다). 버튼을 누르면 그 부대
    /// 인덱스로 SoldierDeploymentPanelUI(병사 선택 그리드)/SquadDeploymentSlotGridUI(4x5 배치
    /// 그리드) 양쪽을 모두 ShowSquad로 갱신한 뒤 squadDetailPopup(별도 팝업, SquadDetailPopup)을
    /// 연다 — "배치 관리" 팝업 자체는 부대 선택 바만 갖고, 실제 편성 UI는 그 위에 한 겹 더 뜨는
    /// 별도 팝업에서 보여주는 구조다(중첩 팝업, section AA/Z와 같은 "안쪽 팝업이 나중 Canvas
    /// sibling이라 위에 그려진다" 관례). 부대의 첫 슬롯이 아직 랭크로 열리지 않았으면 그 부대
    /// 버튼 전체를 비활성화(회색)해 "부대가 비어있다"와 "부대가 아직 잠겨있다"를 구분한다.
    /// </summary>
    public sealed class SoldierSquadSelectorUI : MonoBehaviour
    {
        [SerializeField]
        private Button[] squadButtons;

        [SerializeField]
        private GameObject[] lockedOverlays;

        [SerializeField]
        private SoldierDeploymentPanelUI deploymentPanel;

        [SerializeField]
        private SquadDeploymentSlotGridUI slotGrid;

        [SerializeField]
        private SquadTacticDropdownUI tacticToggle;

        [SerializeField]
        private SoldierDeploymentPopupUI squadDetailPopup;

        private void Awake()
        {
            for (int i = 0; i < squadButtons.Length; i++)
            {
                int squadIndex = i;
                squadButtons[i].onClick.AddListener(() => OnSquadButtonClicked(squadIndex));
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            RefreshLockStates();
            deploymentPanel.ShowSquad(0);
            slotGrid.ShowSquad(0);
            tacticToggle?.ShowSquad(0);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            RefreshLockStates();
        }

        private void OnSquadButtonClicked(int squadIndex)
        {
            deploymentPanel.ShowSquad(squadIndex);
            slotGrid.ShowSquad(squadIndex);
            tacticToggle?.ShowSquad(squadIndex);
            squadDetailPopup.Open();
        }

        /// <summary>
        /// 부대 버튼마다 그 부대의 첫 슬롯(squadIndex * SlotsPerSquad)이 현재 랭크로 열려있는지
        /// 확인해 interactable/잠금 오버레이를 갱신한다.
        /// </summary>
        private void RefreshLockStates()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            int unlockedCount = deployment.GetMaxUnlockedSlotCount();

            for (int i = 0; i < squadButtons.Length; i++)
            {
                bool unlocked = i * SoldierDeploymentService.SlotsPerSquad < unlockedCount;
                squadButtons[i].interactable = unlocked;

                if (lockedOverlays != null && i < lockedOverlays.Length && lockedOverlays[i] != null)
                {
                    lockedOverlays[i].SetActive(!unlocked);
                }
            }
        }
    }
}
