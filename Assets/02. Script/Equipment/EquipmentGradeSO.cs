using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 등급 사다리의 한 단계(예: "커먼1")를 나타내는 데이터 에셋.
    /// 대분류(커먼/언커먼/...)와 그 안의 세부 단계는 별도 필드로 나누지 않고,
    /// EquipmentGradeCatalogSO 안에서의 배열 순서 자체가 곧 등급 순서다 —
    /// StageCatalogSO가 스테이지 순서를 배열 순서로 관리하는 것과 동일한 패턴.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentGrade", menuName = "Idle Project/Equipment/Equipment Grade")]
    public sealed class EquipmentGradeSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private Color tintColor = Color.white;

        /// <summary>
        /// 화면에 표시할 등급 이름 (예: "커먼1").
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 등급의 장비를 표시할 때 사용할 강조 색상.
        /// </summary>
        public Color TintColor => tintColor;
    }
}
