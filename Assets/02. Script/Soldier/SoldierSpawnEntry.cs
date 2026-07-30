using System;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 하나의 스폰 지점에 어떤 Soldier 프리팹을 배치할지 정의한다.
    /// 지점마다 다른 프리팹(근접/원거리 등)을 지정할 수 있다.
    /// </summary>
    [Serializable]
    public sealed class SoldierSpawnEntry
    {
        [SerializeField]
        private GameObject soldierPrefab;

        [SerializeField]
        private Transform spawnPoint;

        /// <summary>
        /// 스폰할 Soldier 프리팹.
        /// </summary>
        public GameObject SoldierPrefab => soldierPrefab;

        /// <summary>
        /// 스폰 위치/회전을 제공하는 지점.
        /// </summary>
        public Transform SpawnPoint => spawnPoint;
    }
}
