using System.Collections.Generic;
using Core;
using Rank.Events;
using Soldier;
using Soldier.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 현재 랭크로 잠금 해제된 배치 슬롯 수(SoldierDeploymentService.GetMaxUnlockedSlotCount)만큼
    /// 행을 보여주는 편성 패널. 슬롯 수가 고정값이 아니라 랭크 승급 시 즉시 늘어난다.
    /// </summary>
    public sealed class SoldierDeploymentPanelUI : MonoBehaviour
    {
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
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Unsubscribe<SoldierRosterChangedEvent>(OnRosterChanged);
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            Refresh();
        }

        private void OnRosterChanged(SoldierRosterChangedEvent evt)
        {
            Refresh();
        }

        private void OnRankChanged(RankChangedEvent evt)
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

            int slotCount = deployment.GetMaxUnlockedSlotCount();

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
