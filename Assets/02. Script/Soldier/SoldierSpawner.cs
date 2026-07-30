using Core;
using Managers;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 엔트리별로 지정된 Soldier 프리팹을 지정된 스폰 지점에 하나씩 배치한다. Rank 해금 등 활성화
    /// 조건은 아직 없으며, 이후 상위 시스템(Rank)이 이 컴포넌트의 활성/비활성을 제어하게 된다.
    /// </summary>
    public sealed class SoldierSpawner : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawnEntry[] entries;

        private void Start()
        {
            if (!GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            foreach (SoldierSpawnEntry entry in entries)
            {
                pool.EnsurePool(entry.SoldierPrefab, 1, 1);
                pool.Get(entry.SoldierPrefab, entry.SpawnPoint.position, entry.SpawnPoint.rotation);
            }
        }
    }
}
