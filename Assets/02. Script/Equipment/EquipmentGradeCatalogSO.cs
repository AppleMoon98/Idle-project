using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 등급 사다리를 진행 순서대로 나열한 데이터 에셋. 합성(Fusion)이
    /// "다음 등급이 무엇인지" 판단할 때 참조한다. StageCatalogSO와 동일한 패턴.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentGradeCatalog", menuName = "Idle Project/Equipment/Equipment Grade Catalog")]
    public sealed class EquipmentGradeCatalogSO : ScriptableObject
    {
        [SerializeField]
        private EquipmentGradeSO[] grades;

        /// <summary>
        /// 진행 순서대로 나열된 등급 목록.
        /// </summary>
        public EquipmentGradeSO[] Grades => grades;

        /// <summary>
        /// grade가 몇 번째(0부터) 등급인지 반환한다. null이거나 목록에 없으면 -1.
        /// </summary>
        public int IndexOf(EquipmentGradeSO grade)
        {
            if (grades == null || grade == null)
            {
                return -1;
            }

            for (int i = 0; i < grades.Length; i++)
            {
                if (grades[i] == grade)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// index 위치의 등급을 반환한다. 범위를 벗어나면 null.
        /// </summary>
        public EquipmentGradeSO GetAt(int index)
        {
            if (grades == null || index < 0 || index >= grades.Length)
            {
                return null;
            }

            return grades[index];
        }

        /// <summary>
        /// current 다음 등급을 반환한다. current가 마지막이거나 목록에 없으면 null.
        /// </summary>
        public EquipmentGradeSO GetNext(EquipmentGradeSO current)
        {
            int index = IndexOf(current);
            return index < 0 ? null : GetAt(index + 1);
        }
    }
}
