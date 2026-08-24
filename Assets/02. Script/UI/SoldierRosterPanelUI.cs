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
    /// 탭하면 SoldierDetailPopupUI를 그 SoldierSO 원형으로 연다(Idle 애니메이션 미리보기 +
    /// 스탯) - 개별 유닛 단위가 아니라 병종+등급 원형 단위라, 스택 안 어느 유닛을 골랐는지는
    /// 무시한다(정의만 넘긴다). 배치는 부대 편성(SoldierDeploymentPanelUI/SquadDeployedPanelUI),
    /// 행동은 부대 편성의 전술(SquadTacticOptionPopupUI) 화면으로 각각 이관돼 로스터에서는 더 이상
    /// 다루지 않는다(병사 전용 장비 시스템이 제거되면서, 슬롯을 탭했을 때 열던
    /// SoldierEquipmentPopupUI/SoldierRosterStackPopupUI가 함께 사라졌다).
    /// 정렬 순서는 (1) 보유 여부(보유 먼저) → (2) 등급(높은 등급 먼저) → (3) 병과 순
    /// (Soldier.SoldierUnitTypeOrder, 부대 편성 목록과 동일한 기준 공유) 고정이다.
    /// </summary>
    public sealed class SoldierRosterPanelUI : MonoBehaviour
    {
        private static readonly List<OwnedSoldier> EmptyStack = new();

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierRosterRowUI rowPrefab;

        [SerializeField]
        private SoldierCatalogSO catalog;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        [SerializeField]
        private SoldierDetailPopupUI detailPopup;

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
                row.Initialize(definition, stack, _ => detailPopup.Open(definition));

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

            return SoldierUnitTypeOrder.IndexOf(a) - SoldierUnitTypeOrder.IndexOf(b);
        }

        private int GradeIndex(SoldierSO definition)
        {
            if (gradeCatalog == null || definition.Grade == null)
            {
                return -1;
            }

            return gradeCatalog.IndexOf(definition.Grade);
        }
    }
}
