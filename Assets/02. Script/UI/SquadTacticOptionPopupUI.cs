using System;
using System.Collections.Generic;
using Core;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 전술 선택 드롭다운 목록 팝업 — SquadTacticDropdownUI가 전술 버튼을 탭하면 연다.
    /// SquadTacticType의 모든 값을 SoldierPickerRowUI("이름+버튼" 형태의 범용 행, section DR에서
    /// 등급 틴트 아이콘까지 지원하도록 확장됨)로 하나씩 나열한다 — 전술 종류가 늘어나도 이
    /// 팝업의 코드는 손댈 필요가 없다, SquadTacticType에 값만 추가하고
    /// SquadTacticDisplayNames에 이름만 채우면 목록에 자동으로 나타난다. 배치가 단일 풀 방식으로
    /// 바뀌면서 부대 인덱스 없이 선택한 전술을 SquadTacticService.SetTacticForAll로 전체 부대에
    /// 동일 적용한다.
    /// </summary>
    public sealed class SquadTacticOptionPopupUI : MonoBehaviour, IDismissible
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private SoldierPickerRowUI rowPrefab;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private Button closeButton;

        private readonly List<SoldierPickerRowUI> _spawnedRows = new();

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// 전술 선택 목록을 연다. current는 목록에서 "(선택됨)" 표시로 구분할 현재 전술이다.
        /// </summary>
        public void Open(SquadTacticType current)
        {
            foreach (SoldierPickerRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            foreach (SquadTacticType tactic in Enum.GetValues(typeof(SquadTacticType)))
            {
                SquadTacticType captured = tactic;
                SoldierPickerRowUI row = Instantiate(rowPrefab, rowContainer);
                string label = SquadTacticDisplayNames.Get(tactic) + (tactic == current ? " (선택됨)" : "");
                row.Initialize(label, () => OnPicked(captured));

                _spawnedRows.Add(row);
            }

            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

        private void OnPicked(SquadTacticType tactic)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SquadTacticService tactics))
            {
                tactics.SetTacticForAll(tactic);
            }

            Close();
        }

        public void Close()
        {
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
