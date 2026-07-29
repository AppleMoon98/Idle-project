using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 하나의 스테이지(예: 1-1)를 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "Stage", menuName = "Idle Project/Stage/Stage")]
    public sealed class StageSO : ScriptableObject
    {
        [SerializeField]
        private int chapter;

        [SerializeField]
        private int stageNumber;

        [SerializeField]
        private MonsterSpawnEntry[] spawnEntries;

        /// <summary>
        /// 챕터 번호 (예: 1-40에서 1).
        /// </summary>
        public int Chapter => chapter;

        /// <summary>
        /// 챕터 내 스테이지 번호 (예: 1-40에서 40).
        /// </summary>
        public int StageNumber => stageNumber;

        /// <summary>
        /// 이 스테이지의 몬스터 스폰 웨이브 목록.
        /// </summary>
        public MonsterSpawnEntry[] SpawnEntries => spawnEntries;
    }
}
