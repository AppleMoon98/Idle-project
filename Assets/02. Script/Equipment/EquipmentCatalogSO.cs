using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 모든 장비 원형을 모아둔 데이터 에셋. 합성(Fusion)이 "이 슬롯의 다음 등급 장비가 무엇인지"
    /// 조회할 때 사용한다. StageCatalogSO/EquipmentGradeCatalogSO와 마찬가지로 배열만 들고,
    /// 조회는 슬롯+등급 조합으로 선형 탐색한다(장비 원형 수가 수백 단위를 넘기 전까지는 충분하다).
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "Idle Project/Equipment/Equipment Catalog")]
    public sealed class EquipmentCatalogSO : ScriptableObject
    {
        [SerializeField]
        private EquipmentSO[] items;

        /// <summary>
        /// 등록된 모든 장비 원형.
        /// </summary>
        public EquipmentSO[] Items => items;

        /// <summary>
        /// 지정한 슬롯·등급 조합의 장비 원형을 반환한다. 없으면(콘텐츠 미비) null.
        /// </summary>
        public EquipmentSO FindBySlotAndGrade(EquipmentType slot, EquipmentGradeSO grade)
        {
            if (items == null || grade == null)
            {
                return null;
            }

            foreach (EquipmentSO item in items)
            {
                if (item != null && item.EquipmentType == slot && item.Grade == grade)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// item이 목록에서 몇 번째(0부터)인지 반환한다. 없으면 -1.
        /// 세이브 데이터가 EquipmentSO 참조 대신 이 인덱스만으로 "어떤 장비인지"를 저장할 때 쓴다.
        /// </summary>
        public int IndexOf(EquipmentSO item)
        {
            if (items == null || item == null)
            {
                return -1;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == item)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// index 위치의 장비 원형을 반환한다. 범위를 벗어나면 null.
        /// </summary>
        public EquipmentSO GetAt(int index)
        {
            if (items == null || index < 0 || index >= items.Length)
            {
                return null;
            }

            return items[index];
        }
    }
}
