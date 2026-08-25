using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 모든 병사 원형을 모아둔 데이터 에셋. EquipmentCatalogSO와 마찬가지로 배열만 들고,
    /// 세이브 데이터가 SoldierSO 참조 대신 이 인덱스만으로 "어떤 병사인지"를 저장할 때 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoldierCatalog", menuName = "Idle Project/Soldier/Soldier Catalog")]
    public sealed class SoldierCatalogSO : ScriptableObject
    {
        [SerializeField]
        private SoldierSO[] soldiers;

        /// <summary>
        /// 등록된 모든 병사 원형.
        /// </summary>
        public SoldierSO[] Soldiers => soldiers;

        /// <summary>
        /// soldier가 목록에서 몇 번째(0부터)인지 반환한다. 없으면 -1.
        /// </summary>
        public int IndexOf(SoldierSO soldier)
        {
            if (soldiers == null || soldier == null)
            {
                return -1;
            }

            for (int i = 0; i < soldiers.Length; i++)
            {
                if (soldiers[i] == soldier)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// index 위치의 병사 원형을 반환한다. 범위를 벗어나면 null.
        /// </summary>
        public SoldierSO GetAt(int index)
        {
            if (soldiers == null || index < 0 || index >= soldiers.Length)
            {
                return null;
            }

            return soldiers[index];
        }

        /// <summary>
        /// stableId가 일치하는 병사 원형을 반환한다. 없거나 stableId가 비어있으면 null
        /// (GitHub 이슈 #19 - EquipmentCatalogSO.FindByStableId와 동일한 이유).
        /// </summary>
        public SoldierSO FindByStableId(string stableId)
        {
            if (soldiers == null || string.IsNullOrEmpty(stableId))
            {
                return null;
            }

            foreach (SoldierSO soldier in soldiers)
            {
                if (soldier != null && soldier.StableId == stableId)
                {
                    return soldier;
                }
            }

            return null;
        }
    }
}
