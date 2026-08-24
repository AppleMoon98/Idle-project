using System.Collections.Generic;
using Core;
using Equipment;
using Rank.Events;
using Soldier;
using Soldier.Events;
using UI.Events;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// "부대 편성" 팝업 하단 — 어디에도 배치되지 않은 채 1마리 이상 남은 병사를 스택 카드로
    /// 보여준다(이미 배치된 유닛/0개 보유 종류는 목록에서 자연히 빠진다). 배치가 더 이상 부대를
    /// 개별 선택하지 않는 단일 풀 방식으로 바뀌면서, 카드를 탭하면 곧장
    /// Soldier.SoldierDeploymentService.TryDeploy로 배치가 확정된다(빈 슬롯을 직접 골라 배치를
    /// 확정하던 SquadDeploymentSlotGridUI는 삭제됨) — 성공하면 그 유닛은 다음 새로고침에서
    /// 스택 개수가 1 줄어들거나(×N 배지) 0이 되면 목록에서 사라지므로, 같은 유닛을 두 번 배치할
    /// 방법 자체가 없다. 실패(코스트 예산 초과/빈 슬롯 없음)하면 토스트로 원인을 안내한다.
    /// 정렬 순서는 (1) 등급(높은 등급 먼저) → (2) 병과 순(Soldier.SoldierUnitTypeOrder, 로스터
    /// 목록과 동일한 기준 공유) 고정이다.
    /// </summary>
    public sealed class SoldierDeploymentPanelUI : MonoBehaviour
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

            var availableByDefinition = new Dictionary<SoldierSO, List<OwnedSoldier>>();

            foreach (OwnedSoldier owned in roster.Roster)
            {
                if (deployment.TryGetSlotOf(owned.InstanceId, out _))
                {
                    continue;
                }

                if (!availableByDefinition.TryGetValue(owned.Definition, out List<OwnedSoldier> stack))
                {
                    stack = new List<OwnedSoldier>();
                    availableByDefinition[owned.Definition] = stack;
                }

                stack.Add(owned);
            }

            var orderedDefinitions = new List<SoldierSO>(availableByDefinition.Keys);
            orderedDefinitions.Sort(CompareDefinitions);

            foreach (SoldierSO definition in orderedDefinitions)
            {
                List<OwnedSoldier> stack = availableByDefinition[definition];

                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(definition, stack, OnStackTapped);

                _spawnedRows.Add(row);
            }
        }

        /// <summary>
        /// 1차 등급(높은 등급 먼저) → 2차 병과(Soldier.SoldierUnitTypeOrder, 로스터 목록과 동일한
        /// 기준 공유) 순으로 정렬한다.
        /// </summary>
        private int CompareDefinitions(SoldierSO a, SoldierSO b)
        {
            int gradeCompare = GradeIndex(b) - GradeIndex(a);
            return gradeCompare != 0 ? gradeCompare : SoldierUnitTypeOrder.IndexOf(a) - SoldierUnitTypeOrder.IndexOf(b);
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
        /// stack(어느 것이든 동일하므로 첫 번째 유닛 기준)을 탭하면 곧장 배치를 시도한다. 실패
        /// 원인별로 다른 토스트 문구를 보여준다.
        /// </summary>
        private void OnStackTapped(IReadOnlyList<OwnedSoldier> stack)
        {
            if (stack.Count == 0 || GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            if (deployment.TryDeploy(stack[0].InstanceId, out DeploymentFailureReason reason))
            {
                return;
            }

            string message = "배치할 수 없습니다.";

            if (reason == DeploymentFailureReason.CostExceeded)
            {
                message = "배치 코스트가 가득 찼습니다.";
            }
            else if (reason == DeploymentFailureReason.NoFreeSlot)
            {
                message = "배치 슬롯이 부족합니다.";
            }

            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent(message));
        }
    }
}
