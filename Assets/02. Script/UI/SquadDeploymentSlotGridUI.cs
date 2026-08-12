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
    /// 선택된 부대(SoldierSquadSelectorUI가 고른 squadIndex)의 실제 배치 슬롯을 보여주고,
    /// 빈 칸을 탭하면 배치를 확정한다. "누구를 배치할지"는 스스로 모른다 — 아래 병사 선택
    /// 패널(SoldierDeploymentPanelUI)에서 미리 선택해둔 SelectedInstanceId를 그대로 소비한다
    /// (선택 → 배치를 두 컴포넌트가 나눠 갖는 구조, 각자 상대방의 존재는 최소한만 안다).
    /// </summary>
    public sealed class SquadDeploymentSlotGridUI : MonoBehaviour
    {
        [SerializeField]
        private SquadDeploymentSlotUI[] slots;

        [SerializeField]
        private SoldierDeploymentPanelUI availablePanel;

        private int _squadIndex;

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

                slots[i].Initialize(slotIndex, isLocked, occupant, OnSlotTapped);
            }
        }

        /// <summary>
        /// 이미 채워진 칸을 탭하면 아무 일도 하지 않는다(해제는 로스터 쪽 "배치 해제" 토글로
        /// 한다, section DH). 빈 칸인데 선택된 유닛이 없으면도 아무 일도 하지 않는다. 선택된
        /// 유닛이 있으면 배정을 확정하고 선택을 지운다. 선택된 유닛이 있는데 이미 이 부대가
        /// 가득 찬 경우는 애초에 빈 칸이 없어 이 메서드 자체가 호출될 수 없으므로 별도 처리가
        /// 필요 없다 — 가득 참 토스트는 그래도 방어적으로 한 번 더 안내한다.
        /// </summary>
        private void OnSlotTapped(int slotIndex)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            if (deployment.TryGetAssigned(slotIndex, out _))
            {
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
