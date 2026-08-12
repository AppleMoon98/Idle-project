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
    /// 선택된 부대(SoldierSquadSelectorUI가 고른 squadIndex)에 배치 가능한 병사를 슬롯 그리드로
    /// 보여주는 패널 — 로스터 그리드(SoldierRosterPanelUI)와 같은 SoldierRosterRowUI를 재사용해
    /// "어디에도 배치되지 않은 채 1마리 이상 남은" 병사 스택만 보여준다(이미 배치된 유닛/0개
    /// 보유 종류는 목록에서 자연히 빠진다). 이제 여기서 직접 배치를 확정하지 않는다 — 슬롯을
    /// 탭하면 "선택" 상태로만 표시되고(테두리, SoldierRosterRowUI.SetSelected), 실제 배치는
    /// SquadDeploymentSlotGridUI(부대 편성 팝업 상단 4x5 그리드)의 빈 칸을 탭하는 순간 확정된다
    /// — 두 컴포넌트가 "선택된 유닛" 하나를 주고받는 형태(SelectedInstanceId/ConsumeSelection).
    /// 배정이 확정되면 그 유닛은 더 이상 "가용"이 아니므로 다음 새로고침에서 스택 개수가 1
    /// 줄어들거나(×N 배지) 0이 되면 목록에서 사라진다 — 같은 유닛을 두 번 배치할 방법 자체가
    /// 없으므로 중복 배치가 구조적으로 불가능하다.
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
        private int _squadIndex;
        private SoldierRosterRowUI _selectedRow;

        /// <summary>
        /// 지금 선택돼 배치를 기다리는 유닛(없으면 null). SquadDeploymentSlotGridUI가 빈 칸을
        /// 탭했을 때 이 값을 읽어 배정을 확정한다.
        /// </summary>
        public int? SelectedInstanceId { get; private set; }

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

        /// <summary>
        /// 표시할 부대를 바꾸고 즉시 새로고침한다. SoldierSquadSelectorUI가 부대 버튼을 탭할 때 호출한다.
        /// </summary>
        public void ShowSquad(int squadIndex)
        {
            _squadIndex = squadIndex;
            Refresh();
        }

        /// <summary>
        /// 선택을 취소한다(테두리도 함께 끈다). SquadDeploymentSlotGridUI가 배치를 확정한 직후
        /// 호출한다 — 다만 배정이 확정되면 이 컴포넌트 자신도 SoldierDeploymentChangedEvent로
        /// Refresh되어 선택된 행 자체가 다시 생성되므로, 이 메서드는 "빈 칸이 없어 배정에
        /// 실패했을 때 선택을 유지할지"를 호출자가 스스로 결정할 수 있도록 공개해둔 것이다.
        /// </summary>
        public void ClearSelection()
        {
            SelectedInstanceId = null;

            if (_selectedRow != null)
            {
                _selectedRow.SetSelected(false);
                _selectedRow = null;
            }
        }

        private void Refresh()
        {
            SelectedInstanceId = null;
            _selectedRow = null;

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
            orderedDefinitions.Sort((a, b) => GradeIndex(b) - GradeIndex(a));

            foreach (SoldierSO definition in orderedDefinitions)
            {
                List<OwnedSoldier> stack = availableByDefinition[definition];

                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(definition, stack, s => OnStackTapped(row, s));

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
        /// stack(어느 것이든 동일하므로 첫 번째 유닛 기준)을 탭했을 때: 이미 이 스택이 선택돼
        /// 있으면 선택을 취소하고(토글), 아니면 이전 선택을 지우고 이 스택을 새로 선택한다.
        /// 실제 배정은 여기서 하지 않는다 — SquadDeploymentSlotGridUI가 빈 칸을 탭할 때
        /// SelectedInstanceId를 읽어 확정한다.
        /// </summary>
        private void OnStackTapped(SoldierRosterRowUI row, IReadOnlyList<OwnedSoldier> stack)
        {
            if (stack.Count == 0)
            {
                return;
            }

            int instanceId = stack[0].InstanceId;

            if (SelectedInstanceId == instanceId)
            {
                ClearSelection();
                return;
            }

            ClearSelection();
            SelectedInstanceId = instanceId;
            _selectedRow = row;
            row.SetSelected(true);
        }
    }
}
