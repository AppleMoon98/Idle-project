using System.Collections.Generic;
using Behavior;
using Core;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 특정 병사 유닛(InstanceId)에 배정할 행동 프로필을 고르는 팝업. 카탈로그의 프로필 목록에
    /// "해제(교전 기본값)" 옵션을 맨 앞에 추가해 보여준다. 행을 고르면 배정하고 닫힌다.
    /// </summary>
    public sealed class SoldierBehaviorProfilePopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierBehaviorProfileRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private BehaviorProfileCatalogSO catalog;

        private int _openInstanceId;
        private readonly List<SoldierBehaviorProfileRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// instanceId 유닛에 배정할 프로필 목록을 채워 팝업을 연다.
        /// </summary>
        public void Open(int instanceId)
        {
            _openInstanceId = instanceId;
            popupRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void Refresh()
        {
            foreach (SoldierBehaviorProfileRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            SoldierBehaviorProfileRowUI unassignRow = Instantiate(rowPrefab, rowContainer);
            unassignRow.Initialize("해제 (기본 교전)", () => OnPicked(null));
            _spawnedRows.Add(unassignRow);

            if (catalog.Profiles == null)
            {
                return;
            }

            foreach (BehaviorProfileSO profile in catalog.Profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                SoldierBehaviorProfileRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(profile.DisplayName, () => OnPicked(profile));

                _spawnedRows.Add(row);
            }
        }

        private void OnPicked(BehaviorProfileSO profile)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierRosterService roster))
            {
                roster.SetBehaviorProfile(_openInstanceId, profile);
            }

            Close();
        }
    }
}
