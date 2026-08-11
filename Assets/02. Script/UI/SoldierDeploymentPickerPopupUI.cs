using System.Collections.Generic;
using Core;
using Equipment;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 특정 배치 슬롯에 배치할 로스터 유닛을 고르는 팝업. 로스터 패널(SoldierRosterPanelUI)과
    /// 같은 방식으로 같은 SoldierSO(등급+병종)를 슬롯 하나에 스택으로 묶어 그리드로 보여준다
    /// (스택이 없으면 애초에 목록에 등장하지 않으므로 "보유 수량 0" 상태는 자연히 걸러진다).
    /// 스택을 고르면 1개면 바로 배치를 확정하고, 2개 이상이면 SoldierDeploymentStackPickerPopupUI로
    /// 개별 유닛을 먼저 고르게 한다. 상단 정렬 드롭다운(등급 높은순/낮은순/보유 수량순)으로
    /// 그리드 순서를 바꿀 수 있다.
    /// </summary>
    public sealed class SoldierDeploymentPickerPopupUI : MonoBehaviour
    {
        private enum SortMode
        {
            GradeDescending,
            GradeAscending,
            CountDescending
        }

        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierRosterRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Dropdown sortDropdown;

        [SerializeField]
        private SoldierDeploymentStackPickerPopupUI stackPickerPopup;

        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        private int _openSlotIndex;
        private readonly List<SoldierRosterRowUI> _spawnedRows = new();
        private readonly Dictionary<SoldierSO, List<OwnedSoldier>> _stacksByDefinition = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);

            if (sortDropdown != null)
            {
                sortDropdown.onValueChanged.AddListener(_ => Refresh());
            }
        }

        /// <summary>
        /// slotIndex에 배치할 유닛을 고르기 위해 로스터 목록을 채워 팝업을 연다. 이미 다른 슬롯에
        /// 배치돼 있는 유닛은 목록에서 제외한다(같은 병사를 여러 슬롯에 중복 배치할 수 없도록).
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
            foreach (SoldierRosterRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();
            _stacksByDefinition.Clear();

            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SoldierRosterService roster)
                || !GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                return;
            }

            var orderedDefinitions = new List<SoldierSO>();

            foreach (OwnedSoldier owned in roster.Roster)
            {
                if (deployment.TryGetSlotOf(owned.InstanceId, out int assignedSlot) && assignedSlot != _openSlotIndex)
                {
                    continue;
                }

                if (!_stacksByDefinition.TryGetValue(owned.Definition, out List<OwnedSoldier> stack))
                {
                    stack = new List<OwnedSoldier>();
                    _stacksByDefinition[owned.Definition] = stack;
                    orderedDefinitions.Add(owned.Definition);
                }

                stack.Add(owned);
            }

            SortMode mode = sortDropdown != null ? (SortMode)sortDropdown.value : SortMode.GradeDescending;
            orderedDefinitions.Sort((a, b) => CompareDefinitions(a, b, mode));

            foreach (SoldierSO definition in orderedDefinitions)
            {
                List<OwnedSoldier> stack = _stacksByDefinition[definition];

                SoldierRosterRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize(definition, stack, OnStackTapped);

                _spawnedRows.Add(row);
            }
        }

        private int CompareDefinitions(SoldierSO a, SoldierSO b, SortMode mode)
        {
            switch (mode)
            {
                case SortMode.GradeAscending:
                    return GradeIndex(a) - GradeIndex(b);
                case SortMode.CountDescending:
                    return _stacksByDefinition[b].Count - _stacksByDefinition[a].Count;
                default:
                    return GradeIndex(b) - GradeIndex(a);
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

        private void OnStackTapped(IReadOnlyList<OwnedSoldier> stack)
        {
            if (stack.Count == 1)
            {
                AssignAndClose(stack[0]);
                return;
            }

            stackPickerPopup.Open(stack, AssignAndClose);
        }

        private void AssignAndClose(OwnedSoldier owned)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
            {
                deployment.TryAssign(_openSlotIndex, owned.InstanceId);
            }

            Close();
        }
    }
}
