using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업(SquadDetailPopup) 제목 줄, 전술 버튼 왼쪽에 그 부대의 현재 배치 인원을
    /// "n/MaxDeployedPerSquad" 형식으로 보여준다. SoldierSquadSelectorUI가 부대 버튼을 탭할
    /// 때마다 ShowSquad로 갱신하고, SoldierDeploymentChangedEvent로 실시간 반영한다.
    /// </summary>
    public sealed class SquadDeploymentCountUI : MonoBehaviour
    {
        [SerializeField]
        private Text countText;

        private int _squadIndex;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
        }

        /// <summary>
        /// 표시할 부대를 바꾸고 즉시 새로고침한다. SoldierSquadSelectorUI가 부대 버튼을 탭할 때 호출한다.
        /// </summary>
        public void ShowSquad(int squadIndex)
        {
            _squadIndex = squadIndex;
            Refresh();
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (countText == null)
            {
                return;
            }

            int occupied = 0;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                occupied = deployment.GetOccupiedCount(_squadIndex);
            }

            countText.text = $"{occupied}/{SoldierDeploymentService.MaxDeployedPerSquad}";
        }
    }
}
