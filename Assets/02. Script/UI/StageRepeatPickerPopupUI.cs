using System.Collections.Generic;
using Core;
using Stage;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// StageModeToggleUI에서 돌파 -> 반복 버튼을 누르면 열리는 팝업. 이 시점엔 아직 모드가 반복으로
    /// 바뀌지 않은 상태다 - 역대 최고 클리어 스테이지를 포함해 그 이하 최대 stageWindowSize개(기본
    /// 20) 스테이지를 최신순(최고 기록부터 내림차순)으로 세로 스크롤 버튼 목록으로 보여주고, 행을
    /// 골라야 비로소 반복 모드로 전환되며 그 스테이지로 즉시 이동한다(StageModeService.SetMode +
    /// StageController.JumpCurrentToStage). 그냥 닫기만 하면(아무것도 안 고르면) 모드는 돌파로
    /// 그대로 남는다. SquadTacticOptionPopupUI(section DS)와 동일한 "버튼 → 세로 스크롤 목록 팝업"
    /// 셸/재사용 행(SoldierPickerRowUI) 패턴을 그대로 따른다.
    /// </summary>
    public sealed class StageRepeatPickerPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private SoldierPickerRowUI rowPrefab;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private int stageWindowSize = 20;

        private readonly List<SoldierPickerRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// 후보 스테이지 목록을 새로 채우고 팝업을 연다.
        /// </summary>
        public void Open()
        {
            foreach (SoldierPickerRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            if (stageController != null)
            {
                foreach (StageSO stage in stageController.GetRepeatableStages(stageWindowSize))
                {
                    StageSO captured = stage;
                    SoldierPickerRowUI row = Instantiate(rowPrefab, rowContainer);
                    row.Initialize($"스테이지 {stage.Chapter}-{stage.StageNumber}", () => OnPicked(captured));

                    _spawnedRows.Add(row);
                }
            }

            popupRoot.SetActive(true);
        }

        private void OnPicked(StageSO stage)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out StageModeService modeService))
            {
                modeService.SetMode(StageProgressionMode.Repeat);
            }

            stageController?.JumpCurrentToStage(stage);
            Close();
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
