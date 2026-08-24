using System.Collections.Generic;
using Dungeon;
using Rank;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 보스 던전 보스 선택 팝업 — 화면 전체를 100% 불투명 배경으로 덮는다. 좌상단 "보스 토벌"
    /// 제목, 우상단 닫기, 그 아래 선택 가능한 승급전 보스 목록(SoldierPickerRowUI 재사용)과
    /// 하나의 입장 버튼. 목록 행을 탭하면 선택만 되고(라벨에 "(선택됨)" 표시), 입장 버튼을
    /// 눌러야 실제로 session.Enter가 호출된다 — Squad.SquadTacticOptionPopupUI가 이미 쓰는
    /// "선택 상태를 라벨 문자열로 표시" 방식과 동일(별도 하이라이트 오버레이 없이 재사용 가능).
    /// 입장 시 popupsToClose(DungeonPopup/IntegratedMenuPopup 등, 이 팝업을 열기까지 거쳐온 상위
    /// 팝업들)도 함께 닫는다 — Gold/Stone/Skill/SoldierRescue 던전의 각 EntryUI가 이미 쓰던
    /// 것과 동일한 관례. 이게 빠져 있던 것이 "보스 토벌 입장 후 던전 목록 팝업이 뒤에 계속 떠
    /// 있는" 버그의 원인이었다.
    /// </summary>
    public sealed class BossDungeonSelectPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button enterButton;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierPickerRowUI rowPrefab;

        [SerializeField]
        private BossDungeonSessionController session;

        [SerializeField]
        private SimplePopupUI[] popupsToClose;

        private readonly List<SoldierPickerRowUI> _spawnedRows = new();

        private RankSO _selectedRank;

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
            enterButton.onClick.AddListener(OnEnterClicked);
        }

        public void Open()
        {
            _selectedRank = null;
            popupRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void Refresh()
        {
            foreach (SoldierPickerRowUI row in _spawnedRows)
            {
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            _spawnedRows.Clear();

            if (session == null)
            {
                return;
            }

            foreach (RankSO rank in session.GetAvailableBosses())
            {
                SoldierPickerRowUI row = Instantiate(rowPrefab, rowContainer);
                RankSO capturedRank = rank;
                string label = rank == _selectedRank ? $"{rank.DisplayName} (선택됨)" : rank.DisplayName;
                row.Initialize(label, () => OnRowSelected(capturedRank));
                _spawnedRows.Add(row);
            }
        }

        private void OnRowSelected(RankSO rank)
        {
            _selectedRank = rank;
            Refresh();
        }

        private void OnEnterClicked()
        {
            if (_selectedRank == null || session == null)
            {
                return;
            }

            RankSO rankToEnter = _selectedRank;
            Close();

            foreach (SimplePopupUI popup in popupsToClose)
            {
                popup.Close();
            }

            session.Enter(rankToEnter);
        }
    }
}
