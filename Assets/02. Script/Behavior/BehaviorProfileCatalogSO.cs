using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// 모든 행동 프로필을 모아둔 데이터 에셋. 다른 카탈로그들과 동일한 shape로,
    /// 세이브 데이터가 BehaviorProfileSO 참조 대신 이 인덱스만으로 "어떤 프로필인지"를 저장할 때 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "BehaviorProfileCatalog", menuName = "Idle Project/Behavior/Behavior Profile Catalog")]
    public sealed class BehaviorProfileCatalogSO : ScriptableObject
    {
        [SerializeField]
        private BehaviorProfileSO[] profiles;

        /// <summary>
        /// 등록된 모든 행동 프로필.
        /// </summary>
        public BehaviorProfileSO[] Profiles => profiles;

        /// <summary>
        /// profile이 목록에서 몇 번째(0부터)인지 반환한다. 없으면 -1.
        /// </summary>
        public int IndexOf(BehaviorProfileSO profile)
        {
            if (profiles == null || profile == null)
            {
                return -1;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] == profile)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// index 위치의 프로필을 반환한다. 범위를 벗어나면 null.
        /// </summary>
        public BehaviorProfileSO GetAt(int index)
        {
            if (profiles == null || index < 0 || index >= profiles.Length)
            {
                return null;
            }

            return profiles[index];
        }

        /// <summary>
        /// stableId가 일치하는 프로필을 반환한다. 없거나 stableId가 비어있으면 null
        /// (GitHub 이슈 #19 - EquipmentCatalogSO.FindByStableId와 동일한 이유).
        /// </summary>
        public BehaviorProfileSO FindByStableId(string stableId)
        {
            if (profiles == null || string.IsNullOrEmpty(stableId))
            {
                return null;
            }

            foreach (BehaviorProfileSO profile in profiles)
            {
                if (profile != null && profile.StableId == stableId)
                {
                    return profile;
                }
            }

            return null;
        }
    }
}
