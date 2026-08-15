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
    /// 아래 병사 선택 패널(SoldierDeploymentPanelUI)에 선택된 병사가 있으면 채워진 칸/빈 칸을
    /// 가리지 않고 이 그리드를 탭하는 즉시 그 병사를 배치(교체)한다 — 최우선 처리. 아래 선택이
    /// 없을 때만 채워진 칸을 탭해 선택 상태(테두리)로 전환하고, 그 상태에서: 같은 칸을 다시 탭하면
    /// 배치를 해제하고, 다른 칸을 탭하면 그 자리로 이동한다(이동하는 칸에 다른 병사가 있으면
    /// 서로 자리를 맞바꾼다 — SoldierDeploymentService.Swap 하나로 이동/스왑을 모두 처리한다).
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
        /// 아래 병사 선택 패널에 선택된 병사(SelectedInstanceId)가 있으면, 채워진 칸이든 빈 칸이든
        /// 항상 그 병사를 이 칸에 배치(이미 다른 병사가 있었다면 교체 - TryAssign이 대상 슬롯을
        /// 덮어쓰면 그 슬롯에서 밀려난 기존 병사는 자동으로 미배치 상태가 된다)하는 것을 최우선으로
        /// 처리한다 - 예전에는 이 검사가 "채워진 칸을 탭하면 그 칸을 그리드 내 이동용으로 선택한다"
        /// 분기보다 뒤에 있어서, 아래에서 병사를 선택한 채 이미 채워진 칸을 누르면 배치 대신
        /// 엉뚱하게 "이 칸을 이동 대상으로 선택"이 되며 아래 선택이 조용히 취소됐다(실사용 중 발견).
        /// 아래 선택이 없을 때만 기존처럼: 같은 슬롯 재탭이면 해제, 다른 슬롯이 이미 선택된 채면
        /// 이동/스왑, 채워진 칸을 새로 탭하면 이동용으로 선택한다.
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

            if (availablePanel != null && availablePanel.SelectedInstanceId != null)
            {
                int instanceId = availablePanel.SelectedInstanceId.Value;

                if (deployment.TryAssign(slotIndex, instanceId))
                {
                    availablePanel.ClearSelection();
                }
                else
                {
                    GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("배치할 수 없습니다."));
                }

                return;
            }

            if (deployment.TryGetAssigned(slotIndex, out _))
            {
                _selectedSlotIndex = slotIndex;
                ApplySelectionHighlight();
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("다른 칸을 누르면 이동, 같은 칸을 다시 누르면 배치가 해제됩니다.", ToastMessageType.Info));
            }
        }
    }
}
