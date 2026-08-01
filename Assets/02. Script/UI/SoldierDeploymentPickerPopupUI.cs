using System.Collections.Generic;
using Core;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 특정 배치 슬롯에 배치할 로스터 유닛을 고르는 팝업. 행을 고르면 그 슬롯에 배정하고 닫힌다.
    /// </summary>
    public sealed class SoldierDeploymentPickerPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierPickerRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        private int _openSlotIndex;
        private readonly List<SoldierPickerRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// slotIndex에 배치할 유닛을 고르기 위해 로스터 목록을 채워 팝업을 연다.
        /// </summary>
        public void Open(int slotIndex)
        {
            _openSlotIndex = slotIndex;
            popupRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void Refresh()
        {
            foreach (SoldierPickerRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierRosterService roster))
            {
                return;
            }

            foreach (OwnedSoldier owned in roster.Roster)
            {
                SoldierPickerRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize($"{owned.Definition.DisplayName} (#{owned.InstanceId})", () => OnPicked(owned.InstanceId));

                _spawnedRows.Add(row);
            }
        }

        private void OnPicked(int instanceId)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                deployment.TryAssign(_openSlotIndex, instanceId);
            }

            Close();
        }
    }
}
