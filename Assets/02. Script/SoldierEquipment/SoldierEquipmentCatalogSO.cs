using UnityEngine;

namespace SoldierEquipment
{
    /// <summary>
    /// 모든 병사 전용 장비 원형을 모아둔 데이터 에셋. EquipmentCatalogSO와 마찬가지로 배열만 들고,
    /// 세이브 데이터가 SoldierEquipmentSO 참조 대신 이 인덱스만으로 "어떤 장비인지"를 저장할 때 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoldierEquipmentCatalog", menuName = "Idle Project/Soldier Equipment/Soldier Equipment Catalog")]
    public sealed class SoldierEquipmentCatalogSO : ScriptableObject
    {
        [SerializeField]
        private SoldierEquipmentSO[] items;

        /// <summary>
        /// 등록된 모든 병사 전용 장비 원형.
        /// </summary>
        public SoldierEquipmentSO[] Items => items;

        /// <summary>
        /// item이 목록에서 몇 번째(0부터)인지 반환한다. 없으면 -1.
        /// </summary>
        public int IndexOf(SoldierEquipmentSO item)
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
        public SoldierEquipmentSO GetAt(int index)
        {
            if (items == null || index < 0 || index >= items.Length)
            {
                return null;
            }

            return items[index];
        }
    }
}
