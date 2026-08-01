using UnityEngine;

namespace Rank
{
    /// <summary>
    /// 랭크를 승급 순서대로 나열한 데이터 에셋. StageCatalogSO/EquipmentGradeCatalogSO와 동일한
    /// 형태(IndexOf/GetAt/GetNext)로, RankService가 "현재 랭크의 다음 랭크"를 조회할 때 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "RankCatalog", menuName = "Idle Project/Rank/Rank Catalog")]
    public sealed class RankCatalogSO : ScriptableObject
    {
        [SerializeField]
        private RankSO[] ranks;

        /// <summary>
        /// 승급 순서대로 나열된 랭크 목록.
        /// </summary>
        public RankSO[] Ranks => ranks;

        /// <summary>
        /// current 다음 순서의 랭크를 반환한다. current가 마지막이거나 목록에 없으면 null.
        /// </summary>
        public RankSO GetNext(RankSO current)
        {
            if (ranks == null)
            {
                return null;
            }

            for (int i = 0; i < ranks.Length - 1; i++)
            {
                if (ranks[i] == current)
                {
                    return ranks[i + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// rank가 목록에서 몇 번째(0부터)인지 반환한다. null이거나 목록에 없으면 -1.
        /// </summary>
        public int IndexOf(RankSO rank)
        {
            if (ranks == null || rank == null)
            {
                return -1;
            }

            for (int i = 0; i < ranks.Length; i++)
            {
                if (ranks[i] == rank)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// index 위치의 랭크를 반환한다. 범위를 벗어나면 null.
        /// </summary>
        public RankSO GetAt(int index)
        {
            if (ranks == null || index < 0 || index >= ranks.Length)
            {
                return null;
            }

            return ranks[index];
        }
    }
}
