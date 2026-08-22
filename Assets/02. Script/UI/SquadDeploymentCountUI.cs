using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업 제목 줄, 전술 버튼 왼쪽에 현재 배치 코스트를 "usedCost/MaxDeploymentCost"
    /// 형식으로 보여준다. 배치가 부대별 인원 관리(옛 "n/MaxDeployedPerSquad")에서 병과별 코스트
    /// 예산 하나로 바뀌면서, 부대 인덱스 없이 전체 배치 코스트 합만 보여주는 것으로 단순화됐다.
    /// SoldierDeploymentChangedEvent로 실시간 반영한다.
    /// </summary>
    public sealed class SquadDeploymentCountUI : MonoBehaviour
    {
        [SerializeField]
        private Text countText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
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

            int usedCost = 0;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                usedCost = deployment.GetTotalDeployedCost();
            }

            countText.text = $"{usedCost}/{SoldierDeploymentService.MaxDeploymentCost}";
        }
    }
}
