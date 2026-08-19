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

        [SerializeField]
        private bool spawnWithTactics;

        /// <summary>
        /// 스폰할 몬스터 프리팹.
        /// </summary>
        public GameObject MonsterPrefab => monsterPrefab;

        /// <summary>
        /// 스폰할 몬스터 수.
        /// </summary>
        public int Count => count;

        /// <summary>
        /// 몬스터 한 마리씩 스폰되는 간격(초). spawnWithTactics가 켜져 있으면 무시된다(전부 즉시 스폰).
        /// </summary>
        public float SpawnInterval => spawnInterval;

        /// <summary>
        /// 스폰 시 무작위로 고를 스프라이트 후보 세트. null이면 프리팹의 기본 스프라이트를 그대로 쓴다.
        /// </summary>
        public MonsterVisualSetSO VisualSet => visualSet;

        /// <summary>
        /// 켜져 있으면 이 웨이브의 Count 전부를 시간차 없이, 전술 웨이브와 같은 틱에 즉시 스폰한다.
        /// spawnEntries 배열의 앞쪽부터 순서대로만 인식된다(MonsterSpawner.SpawnImmediateEntries 참고) -
        /// 중간에 일반 항목이 끼어있으면 그 뒤는 인식되지 않는다.
        /// </summary>
        public bool SpawnWithTactics => spawnWithTactics;
    }
}
