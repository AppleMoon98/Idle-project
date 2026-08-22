using System.Collections.Generic;
using Core;
using Equipment;
using Rank.Events;
using Soldier;
using Soldier.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// "부대 편성" 팝업 상단 — 현재 배치돼 있는 병사 전체를 하단(SoldierDeploymentPanelUI)과
    /// 정확히 같은 스택 카드 방식으로 보여준다. 옛 4x5 슬롯 그리드(SquadDeploymentSlotGridUI,
    /// 삭제됨)를 대체 — 배치가 부대별 슬롯 선택에서 단일 풀 방식으로 바뀌면서, 어느 슬롯에
    /// 배치돼 있는지는 더 이상 화면에 드러나지 않고 "배치돼 있다/아니다"만 구분한다. 카드를
    /// 탭하면 곧장 Soldier.SoldierDeploymentService.TryUndeploy로 배치를 해제해 하단 목록으로
    /// 돌려보낸다.
    /// </summary>
    public sealed class SquadDeployedPanelUI : MonoBehaviour
    {
        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierRosterRowUI rowPrefab;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        private readonly List<SoldierRosterRowUI> _spawnedRows = new();

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
            foreach (SoldierRosterRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SoldierRosterService roster)
                || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            var deployedByDefinition = new Dictionary<SoldierSO, List<OwnedSoldier>>();

            foreach (OwnedSoldier owned in roster.Roster)
            {
                if (!deployment.TryGetSlotOf(owned.InstanceId, out _))
                {
                    continue;
                }

                if (!deployedByDefinition.TryGetValue(owned.Definition, out List<OwnedSoldier> stack))
                {
                    stack = new List<OwnedSoldier>();
                    deployedByDefinition[owned.Definition] = stack;
                }

                stack.Add(owned);
            }

            var orderedDefinitions = new List<SoldierSO>(deployedByDefinition.Keys);
            orderedDefinitions.Sort((a, b) => GradeIndex(b) - GradeIndex(a));

            foreach (SoldierSO definition in orderedDefinitions)
            {
                List<OwnedSoldier> stack = deployedByDefinition[definition];

                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(definition, stack, OnStackTapped);

                _spawnedRows.Add(row);
            }
        }

        private int GradeIndex(SoldierSO definition)
        {
            if (gradeCatalog == null || definition.Grade == null)
            {
                return -1;
            }

            return gradeCatalog.IndexOf(definition.Grade);
        }

        /// <summary>
        /// stack(어느 것이든 동일하므로 첫 번째 유닛 기준)을 탭하면 곧장 배치를 해제한다.
        /// </summary>
        private void OnStackTapped(IReadOnlyList<OwnedSoldier> stack)
        {
            if (stack.Count == 0 || GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            deployment.TryUndeploy(stack[0].InstanceId);
        }
    }
}
