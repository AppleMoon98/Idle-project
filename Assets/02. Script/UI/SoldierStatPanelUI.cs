using Core;
using Enhancement;
using SoldierEnhancement;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// SoldierEnhancementService에 설정된 능력치 종류만큼 SoldierStatRowUI를 생성해 배치한다.
    /// UI.StatPanelUI(플레이어용)와 동일한 구조의 병렬 컴포넌트다.
    /// </summary>
    public sealed class SoldierStatPanelUI : MonoBehaviour
    {
        [SerializeField]
        private SoldierStatRowUI rowPrefab;

        [SerializeField]
        private Transform rowParent;

        private void Awake()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierEnhancementService service))
            {
                return;
            }

            foreach (EnhancementStatType statType in service.StatTypes)
            {
                SoldierStatRowUI row = Instantiate(rowPrefab, rowParent);
                row.Initialize(statType);
            }
        }
    }
}
