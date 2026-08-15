using System;
using System.Collections.Generic;
using Core;
using Equipment;
using Soldier;
using Soldier.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 병사 로스터 전체(카탈로그의 모든 종류)를 슬롯 그리드로 보여준다. 같은 SoldierSO(등급+병종)를
    /// 가진 유닛은 장비 인벤토리처럼 슬롯 하나에 개수로 쌓인다(SoldierRosterRowUI). 아직 한 마리도
    /// 뽑지 못한 종류도 슬롯으로 함께 표시하되 회색 비활성 상태로 보여준다(0개 보유 스택). 슬롯을
    /// 탭하면 보유 스택이 1개면 바로 SoldierEquipmentPopupUI를 그 병사로 열고, 2개 이상이면
    /// 먼저 SoldierRosterStackPopupUI로 개별 유닛을 고르게 한 뒤 그 유닛으로 장비 팝업을 연다.
    /// 배치는 배치 관리(SquadDeploymentSlotGridUI), 행동은 부대 편성의 전술(SquadTacticOptionPopupUI)
    /// 화면으로 각각 이관돼 로스터에서는 더 이상 다루지 않는다 - 예전엔 배치/장비/행동을 고르는
    /// SoldierRosterSlotActionPopupUI를 한 번 더 거쳤지만, 남은 액션이 장비 하나뿐이라 그 선택
    /// 단계 자체를 없애고 곧장 장비 팝업으로 이동한다.
    /// 정렬 순서는 (1) 보유 여부(보유 먼저) → (2) 등급(높은 등급 먼저) → (3) 병과 순(로드맵 순서:
    /// 보병/궁병/기마궁수/기마병/기사/창병/방패보병/공성병) 고정이다.
    /// </summary>
    public sealed class SoldierRosterPanelUI : MonoBehaviour
    {
        private static readonly string[] UnitTypeOrder =
        {
            "보병", "궁병", "기마궁수", "기마병", "기사", "창병", "방패보병", "공성병"
        };

        private static readonly List<OwnedSoldier> EmptyStack = new();

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierRosterRowUI rowPrefab;

        [SerializeField]
        private SoldierEquipmentPopupUI equipmentPopup;

        [SerializeField]
        private SoldierRosterStackPopupUI stackPopup;

        [SerializeField]
        private SoldierCatalogSO catalog;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

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

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierRosterService roster) || catalog == null)
            {
                return;
            }

            var stacksByDefinition = new Dictionary<SoldierSO, List<OwnedSoldier>>();

            foreach (OwnedSoldier owned in roster.Roster)
            {
                if (!stacksByDefinition.TryGetValue(owned.Definition, out List<OwnedSoldier> stack))
                {
                    stack = new List<OwnedSoldier>();
                    stacksByDefinition[owned.Definition] = stack;
                }

                stack.Add(owned);
            }

            var orderedDefinitions = new List<SoldierSO>();

            foreach (SoldierSO definition in catalog.Soldiers)
            {
                if (definition != null)
                {
                    orderedDefinitions.Add(definition);
                }
            }

            orderedDefinitions.Sort((a, b) => CompareDefinitions(a, b, stacksByDefinition));

            foreach (SoldierSO definition in orderedDefinitions)
            {
                if (!stacksByDefinition.TryGetValue(definition, out List<OwnedSoldier> stack))
                {
                    stack = EmptyStack;
                }

                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(definition, stack, OnSlotTapped);

                _spawnedRows.Add(row);
            }
        }

        private int CompareDefinitions(SoldierSO a, SoldierSO b, Dictionary<SoldierSO, List<OwnedSoldier>> stacksByDefinition)
        {
            bool ownedA = stacksByDefinition.TryGetValue(a, out List<OwnedSoldier> stackA) && stackA.Count > 0;
            bool ownedB = stacksByDefinition.TryGetValue(b, out List<OwnedSoldier> stackB) && stackB.Count > 0;

            if (ownedA != ownedB)
            {
                return ownedA ? -1 : 1;
            }

            int gradeCompare = GradeIndex(b) - GradeIndex(a);
            if (gradeCompare != 0)
            {
                return gradeCompare;
            }

            return UnitTypeIndex(a) - UnitTypeIndex(b);
        }

        private int GradeIndex(SoldierSO definition)
        {
            if (gradeCatalog == null || definition.Grade == null)
            {
                return -1;
            }

            return gradeCatalog.IndexOf(definition.Grade);
        }

        private static int UnitTypeIndex(SoldierSO definition)
        {
            string name = definition.DisplayName;
            int spaceIndex = name.LastIndexOf(' ');
            string unitType = spaceIndex >= 0 ? name.Substring(spaceIndex + 1) : name;

            int index = Array.IndexOf(UnitTypeOrder, unitType);
            return index >= 0 ? index : UnitTypeOrder.Length;
        }

        private void OnSlotTapped(IReadOnlyList<OwnedSoldier> stack)
        {
            if (stack.Count == 0)
            {
                return;
            }

            if (stack.Count == 1)
            {
                equipmentPopup.Open(stack[0].InstanceId);
                return;
            }

            stackPopup.Open(stack);
        }
    }
}
