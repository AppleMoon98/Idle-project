using System.Collections.Generic;
using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 보유 병사 로스터 전체를 행 목록으로 보여준다. 행의 "장비"/"행동" 버튼을 누르면
    /// 각각 SoldierEquipmentPopupUI/SoldierBehaviorProfilePopupUI를 그 병사의 InstanceId로 연다.
    /// </summary>
    public sealed class SoldierRosterPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierRosterRowUI rowPrefab;

        [SerializeField]
        private SoldierEquipmentPopupUI equipmentPopup;

        [SerializeField]
        private SoldierBehaviorProfilePopupUI behaviorProfilePopup;

        private readonly List<SoldierRosterRowUI> _spawnedRows = new();

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SoldierRosterChangedEvent>(OnRosterChanged);
            GameBootstrapper.Events?.Subscribe<SoldierBehaviorProfileChangedEvent>(OnBehaviorProfileChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierRosterChangedEvent>(OnRosterChanged);
            GameBootstrapper.Events?.Unsubscribe<SoldierBehaviorProfileChangedEvent>(OnBehaviorProfileChanged);
        }

        private void OnRosterChanged(SoldierRosterChangedEvent evt)
        {
            Refresh();
        }

        private void OnBehaviorProfileChanged(SoldierBehaviorProfileChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            foreach (SoldierRosterRowUI row in _spawnedRows)
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
                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(owned, instanceId => equipmentPopup.Open(instanceId), instanceId => behaviorProfilePopup.Open(instanceId));

                _spawnedRows.Add(row);
            }
        }
    }
}
