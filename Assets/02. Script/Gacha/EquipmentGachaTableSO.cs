using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 무기 가챠 한 티어(일반/고급/유료 등)의 확률 테이블과 골드 소모량을 정의하는 데이터 에셋.
    /// GachaTableSO(병사)와 대칭되는 구조.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentGachaTable", menuName = "Idle Project/Gacha/Equipment Gacha Table")]
    public sealed class EquipmentGachaTableSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private EquipmentGachaPoolEntry[] entries;

        [SerializeField]
        [Min(0)]
        private int goldCostPerPull;

        /// <summary>
        /// 이 티어의 표시 이름(가챠 팝업 하위 탭 라벨용, 예: "일반 뽑기").
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 테이블의 확률 항목 목록.
        /// </summary>
        public EquipmentGachaPoolEntry[] Entries => entries;

        /// <summary>
        /// 1회 뽑기에 소모되는 골드.
        /// </summary>
        public int GoldCostPerPull => goldCostPerPull;
    }
}
