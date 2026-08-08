using System;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 내 하나의 전술(Tactic) 웨이브를 정의한다. MonsterSpawnEntry와 달리 한 종류의
    /// 프리팹을 N마리 스폰하는 게 아니라, 리더(leaderPrefab)와 추종자(followerPrefab)를
    /// pairCount쌍만큼 짝지어 스폰한다 - 실제 페어링/재배정 로직은 type에 대응하는
    /// Stage.Tactics.ITacticSpawnStrategy가 담당한다.
    /// </summary>
    [Serializable]
    public sealed class TacticSpawnEntry
    {
        [SerializeField]
        private TacticType type;

        [SerializeField]
        private GameObject leaderPrefab;

        [SerializeField]
        private GameObject followerPrefab;

        [SerializeField]
        private GameObject alternateFollowerPrefab;

        [SerializeField]
        [Range(0f, 1f)]
        private float alternateFollowerChance;

        [SerializeField]
        private int pairCount;

        [SerializeField]
        private float spawnInterval;

        /// <summary>
        /// 전술 종류.
        /// </summary>
        public TacticType Type => type;

        /// <summary>
        /// 대형의 1열(예: 방패병).
        /// </summary>
        public GameObject LeaderPrefab => leaderPrefab;

        /// <summary>
        /// 대형의 2열(예: 창병).
        /// </summary>
        public GameObject FollowerPrefab => followerPrefab;

        /// <summary>
        /// 2열 자리에 followerPrefab 대신 가끔 들어가는 대체 유닛(예: 궁병). null이면 대체 없음.
        /// </summary>
        public GameObject AlternateFollowerPrefab => alternateFollowerPrefab;

        /// <summary>
        /// 2열 한 자리를 스폰할 때마다 alternateFollowerPrefab이 대신 뽑힐 확률(0~1).
        /// </summary>
        public float AlternateFollowerChance => alternateFollowerChance;

        /// <summary>
        /// 스폰할 쌍의 수.
        /// </summary>
        public int PairCount => pairCount;

        /// <summary>
        /// 쌍 하나씩 스폰되는 간격(초).
        /// </summary>
        public float SpawnInterval => spawnInterval;
    }
}
