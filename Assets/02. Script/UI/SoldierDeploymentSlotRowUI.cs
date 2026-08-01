using System;
using Core;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 배치 슬롯 한 칸을 표시/제어한다. 현재 배정된 유닛 이름(없으면 "비어있음")을 보여주고,
    /// 배치 버튼으로 피커 팝업을 열거나 해제 버튼으로 즉시 배정을 해제할 수 있다.
    /// </summary>
    public sealed class SoldierDeploymentSlotRowUI : MonoBehaviour
    {
        [SerializeField]
        private Text label;

        [SerializeField]
        private Button assignButton;

        [SerializeField]
        private Button unassignButton;

        private int _slotIndex;

        /// <summary>
        /// 행 데이터를 채운다. onAssignRequested는 배치 버튼을 눌렀을 때 이 슬롯의 인덱스로
        /// 피커 팝업을 열어달라는 요청 콜백이다.
        /// </summary>
        public void Initialize(int slotIndex, OwnedSoldier assigned, Action<int> onAssignRequested)
        {
            _slotIndex = slotIndex;

            string assignedText = assigned != null ? $"{assigned.Definition.DisplayName} (#{assigned.InstanceId})" : "비어있음";
            label.text = $"슬롯 {slotIndex}: {assignedText}";

            assignButton.onClick.AddListener(() => onAssignRequested?.Invoke(_slotIndex));

            unassignButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierDeploymentService deployment))
                {
                    deployment.Unassign(_slotIndex);
                }
            });
        }
    }
}
