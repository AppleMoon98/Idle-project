using System.Collections.Generic;
using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 고정된 배치 슬롯 수(slotCount)만큼 행을 보여주는 편성 패널. 씬의 SoldierSpawner에 구성된
    /// 슬롯 수와 반드시 일치해야 한다(슬롯 수 자체는 서비스로 조회할 방법이 없어 수동으로 맞춘다 —
    /// GameBootstrapper.soldierCount와 같은 성격의 값).
    /// </summary>
    public sealed class SoldierDeploymentPanelUI : MonoBehaviour
    {
        [SerializeField]
        private int slotCount = 2;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierDeploymentSlotRowUI rowPrefab;

        [SerializeField]
        private SoldierDeploymentPickerPopupUI pickerPopup;

        private readonly List<SoldierDeploymentSlotRowUI> _spawnedRows = new();

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Subscribe<SoldierRosterChangedEvent>(OnRosterChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Unsubscribe<SoldierRosterChangedEvent>(OnRosterChanged);
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            Refresh();
        }

        private void OnRosterChanged(SoldierRosterChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            foreach (SoldierDeploymentSlotRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                deployment.TryGetAssigned(slotIndex, out OwnedSoldier assigned);

                SoldierDeploymentSlotRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(slotIndex, assigned, index => pickerPopup.Open(index));

                _spawnedRows.Add(row);
            }
        }
    }
}
