using Core;
using Rank.Events;
using Soldier;
using Soldier.Events;
using UI.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업 상단의 4x5(SoldierDeploymentService.SlotsPerSquad개) 배치 그리드 —
    /// 선택된 부대(SoldierSquadSelectorUI가 고른 squadIndex)의 실제 배치 슬롯을 보여준다.
    /// 채워진 칸을 탭하면 선택 상태(테두리)로 전환되고, 그 상태에서: 같은 칸을 다시 탭하면
    /// 배치를 해제하고, 다른 칸을 탭하면 그 자리로 이동한다(이동하는 칸에 다른 병사가 있으면
    /// 서로 자리를 맞바꾼다 — SoldierDeploymentService.Swap 하나로 이동/스왑을 모두 처리한다).
    /// 선택된 슬롯이 없는 상태에서 빈 칸을 탭하는 경우만 "누구를 배치할지"를 스스로 모르는 채
    /// 아래 병사 선택 패널(SoldierDeploymentPanelUI)이 미리 골라둔 SelectedInstanceId를 그대로
    /// 소비해 새로 배치한다.
    /// </summary>
    public sealed class SquadDeploymentSlotGridUI : MonoBehaviour
    {
        [SerializeField]
        private SquadDeploymentSlotUI[] slots;

        [SerializeField]
        private SoldierDeploymentPanelUI availablePanel;

        private int _squadIndex;
        private int _selectedSlotIndex = -1;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            Refresh();
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            Refresh();
        }

        /// <summary>
        /// 표시할 부대를 바꾸고 즉시 새로고침한다. SoldierSquadSelectorUI가 부대 버튼을 탭할 때 호출한다.
        /// </summary>
        public void ShowSquad(int squadIndex)
        {
            _squadIndex = squadIndex;
            _selectedSlotIndex = -1;
            Refresh();
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            int unlockedCount = deployment.GetMaxUnlockedSlotCount();
            int firstSlotIndex = _squadIndex * SoldierDeploymentService.SlotsPerSquad;

            for (int i = 0; i < slots.Length; i++)
            {
                int slotIndex = firstSlotIndex + i;
                bool isLocked = slotIndex >= unlockedCount;
                deployment.TryGetAssigned(slotIndex, out OwnedSoldier occupant);

                slots[i].Initialize(slotIndex, i + 1, isLocked, occupant, OnSlotTapped);
            }

            ApplySelectionHighlight();
        }

        /// <summary>
        /// _selectedSlotIndex 하나만 보고 그리드 전체의 선택 테두리를 다시 그린다 — Refresh()가
        /// 슬롯 데이터를 새로 채운 직후, 그리고 선택 상태만 바뀌고 데이터는 그대로일 때(첫 탭으로
        /// 선택을 거는 순간) 양쪽에서 호출한다.
        /// </summary>
        private void ApplySelectionHighlight()
        {
            int firstSlotIndex = _squadIndex * SoldierDeploymentService.SlotsPerSquad;

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].SetSelected(firstSlotIndex + i == _selectedSlotIndex);
            }
        }

        /// <summary>
        /// 채워진 칸을 탭하면 선택 상태(테두리)로 전환한다. 이미 선택된 슬롯이 있는 상태에서
        /// 탭하면: 같은 슬롯이면 배치 해제, 다른 슬롯이면 그 자리로 이동/스왑한다
        /// (SoldierDeploymentService.Swap이 대상 칸이 비어있든 채워져 있든 동일하게 처리한다).
        /// 선택된 슬롯이 없는 채로 빈 칸을 탭했을 때만 아래 병사 선택 패널의 SelectedInstanceId를
        /// 소비해 새로 배치한다.
        /// </summary>
        private void OnSlotTapped(int slotIndex)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            if (_selectedSlotIndex == slotIndex)
            {
                _selectedSlotIndex = -1;
                deployment.Unassign(slotIndex);
                ApplySelectionHighlight();
                return;
            }

            if (_selectedSlotIndex != -1)
            {
                int sourceSlotIndex = _selectedSlotIndex;
                _selectedSlotIndex = -1;
                deployment.Swap(sourceSlotIndex, slotIndex);
                ApplySelectionHighlight();
                return;
            }

            if (deployment.TryGetAssigned(slotIndex, out _))
            {
                _selectedSlotIndex = slotIndex;
                availablePanel?.ClearSelection();
                ApplySelectionHighlight();
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("다른 칸을 누르면 이동, 같은 칸을 다시 누르면 배치가 해제됩니다."));
                return;
            }

            if (availablePanel == null || availablePanel.SelectedInstanceId == null)
            {
                return;
            }

            int instanceId = availablePanel.SelectedInstanceId.Value;

            if (deployment.TryAssign(slotIndex, instanceId))
            {
                availablePanel.ClearSelection();
            }
            else
            {
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("배치할 수 없습니다."));
            }
        }
    }
}
