using System;
using Character;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 내 하나의 몬스터 스폰 웨이브를 정의한다.
    /// </summary>
    [Serializable]
    public sealed class MonsterSpawnEntry
    {
        [SerializeField]
        private GameObject monsterPrefab;

        [SerializeField]
        private int count;

        [SerializeField]
        private float spawnInterval;

        [SerializeField]
        private MonsterVisualSetSO visualSet;

        /// <summary>
        /// 스폰할 몬스터 프리팹.
        /// </summary>
        public GameObject MonsterPrefab => monsterPrefab;

        /// <summary>
        /// 스폰할 몬스터 수.
        /// </summary>
        public int Count => count;

        /// <summary>
        /// 몬스터 한 마리씩 스폰되는 간격(초).
        /// </summary>
        public float SpawnInterval => spawnInterval;

        /// <summary>
        /// 스폰 시 무작위로 고를 스프라이트 후보 세트. null이면 프리팹의 기본 스프라이트를 그대로 쓴다.
        /// </summary>
        public MonsterVisualSetSO VisualSet => visualSet;
    }
}
