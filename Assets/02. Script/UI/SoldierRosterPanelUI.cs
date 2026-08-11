using System.Collections.Generic;
using Core;
using Soldier;
using Soldier.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 보유 병사 로스터 전체를 슬롯 그리드로 보여준다. 같은 SoldierSO(등급+병종)를 가진 유닛은
    /// 장비 인벤토리처럼 슬롯 하나에 개수로 쌓인다(SoldierRosterRowUI). 슬롯을 탭하면 스택이
    /// 1개면 바로 SoldierRosterSlotActionPopupUI를 그 병사로 열고, 2개 이상이면 먼저
    /// SoldierRosterStackPopupUI로 개별 유닛을 고르게 한 뒤 그 유닛으로 액션 팝업을 연다.
    /// </summary>
    public sealed class SoldierRosterPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierRosterRowUI rowPrefab;

        [SerializeField]
        private SoldierRosterSlotActionPopupUI actionPopup;

        [SerializeField]
        private SoldierRosterStackPopupUI stackPopup;

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

            var stacksByDefinition = new Dictionary<SoldierSO, List<OwnedSoldier>>();
            var orderedDefinitions = new List<SoldierSO>();

            foreach (OwnedSoldier owned in roster.Roster)
            {
                if (!stacksByDefinition.TryGetValue(owned.Definition, out List<OwnedSoldier> stack))
                {
                    stack = new List<OwnedSoldier>();
                    stacksByDefinition[owned.Definition] = stack;
                    orderedDefinitions.Add(owned.Definition);
                }

                stack.Add(owned);
            }

            foreach (SoldierSO definition in orderedDefinitions)
            {
                List<OwnedSoldier> stack = stacksByDefinition[definition];

                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(stack, OnSlotTapped);

                _spawnedRows.Add(row);
            }
        }

        private void OnSlotTapped(IReadOnlyList<OwnedSoldier> stack)
        {
            if (stack.Count == 1)
            {
                actionPopup.Open(stack[0]);
                return;
            }

            stackPopup.Open(stack);
        }
    }
}
