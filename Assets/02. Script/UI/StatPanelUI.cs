using Core;
using Enhancement;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// EnhancementService에 설정된 능력치 종류만큼 StatRowUI를 생성해 배치한다.
    /// 새 능력치는 EnhancementConfigSO 에셋 추가만으로 이 패널에 자동으로 행이 늘어난다.
    /// </summary>
    public sealed class StatPanelUI : MonoBehaviour
    {
        [SerializeField]
        private StatRowUI rowPrefab;

        [SerializeField]
        private Transform rowParent;

        private void Awake()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out EnhancementService service))
            {
                return;
            }

            foreach (EnhancementStatType statType in service.StatTypes)
            {
                StatRowUI row = Instantiate(rowPrefab, rowParent);
                row.Initialize(statType);
            }
        }
    }
}
