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

        // GitHub 이슈 #27 - MonsterSpawner.TickEntries/SpawnImmediateEntries 둘 다 이 값을 전혀
        // 읽지 않는다(spawnWithTactics 여부와 무관하게 웨이브 전체가 항상 한 틱에 즉시 스폰된다).
        // "일반 웨이브는 시간차, spawnWithTactics는 즉시"라던 예전 설계가 바뀐 뒤에도 이 필드와
        // SpawnInterval 프로퍼티의 문서만 갱신되지 않고 남아있었다 - 기존 스테이지 데이터 호환을
        // 위해 필드 자체는 남겨두되(이미 저장된 320개 스테이지 에셋을 건드리지 않기 위함), 값은
        // 순수하게 무시되는 죽은 데이터다.
        [Tooltip("사용되지 않음(GitHub 이슈 #27) - MonsterSpawner가 이 웨이브 전체를 항상 한 틱에 즉시 스폰하므로 이 값은 아무 효과가 없다. 기존 스테이지 데이터 호환을 위해서만 필드가 남아있다.")]
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
        /// 사용되지 않는 죽은 필드(GitHub 이슈 #27) - MonsterSpawner는 spawnWithTactics 값과 무관하게
        /// 이 웨이브의 Count 전부를 항상 한 틱에 즉시 스폰하며, 이 값을 전혀 읽지 않는다. 기존
        /// 스테이지 데이터(SO 에셋) 호환을 위해 필드만 남겨뒀다 - 새 콘텐츠에서 값을 채워도 아무
        /// 효과가 없다.
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
