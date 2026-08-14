using System;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 내 하나의 전술(Tactic) 웨이브를 정의한다. MonsterSpawnEntry와 달리 한 종류의
    /// 프리팹을 N마리 스폰하는 게 아니라, 리더(leaderPrefab)와 추종자(followerPrefab)를
    /// totalUnitCount / 2쌍만큼 짝지어 pairSpawnInterval 간격으로 스폰한다 - "이 웨이브에서
    /// 잡아야 하는 유닛 수"를 그대로 담는 값이 totalUnitCount이고(1열+2열 합산), 절반씩 나눠
    /// 리더/추종자가 된다 - 실제 페어링/재배정 로직은 type에 대응하는
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
        private int totalUnitCount;

        [SerializeField]
        private float pairSpawnInterval;

        [SerializeField]
        private float immediateEntryDelay;

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
        /// 이 웨이브에서 잡아야 하는 총 유닛 수(1열+2열 합산). 절반이 리더, 절반이 추종자로
        /// 스폰된다.
        /// </summary>
        public int TotalUnitCount => totalUnitCount;

        /// <summary>
        /// 쌍(리더+추종자) 하나가 스폰된 뒤 다음 쌍이 스폰되기까지의 간격(초). 0(기본값)이면
        /// 기존과 동일하게 모든 쌍이 한 틱에 동시 스폰된다 - 41마리가 한꺼번에 교전 가능해지는
        /// 상황(예: N-40 방패벽)을 완화하고 싶을 때만 0보다 크게 설정한다.
        /// </summary>
        public float PairSpawnInterval => pairSpawnInterval;

        /// <summary>
        /// 이 전술 웨이브(마지막 전술 엔트리일 때만 의미 있음)의 대형이 다 갖춰진 뒤,
        /// spawnWithTactics로 묶인 일반 웨이브(예: N-40의 기마병/보스)를 스폰하기까지 기다리는
        /// 시간(초). 0(기본값)이면 기존과 동일하게 대형이 갖춰지자마자(같은 프레임 내) 바로
        /// 스폰된다.
        /// </summary>
        public float ImmediateEntryDelay => immediateEntryDelay;
    }
}
