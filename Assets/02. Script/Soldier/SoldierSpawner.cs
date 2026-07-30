using Core;
using Managers;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 엔트리별로 지정된 Soldier 프리팹을 지정된 스폰 지점에 하나씩 배치한다. Rank 해금 등 활성화
    /// 조건은 아직 없으며, 이후 상위 시스템(Rank)이 이 컴포넌트의 활성/비활성을 제어하게 된다.
    /// 스폰한 병사가 사망하면 SoldierRespawner를 통해 respawnDelay 후 같은 자리에서 다시 소환한다.
    /// </summary>
    public sealed class SoldierSpawner : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawnEntry[] entries;

        [SerializeField]
        private float respawnDelay = 5f;

        private SoldierRespawner _respawner;

        private void Start()
        {
            if (!GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            _respawner = new SoldierRespawner(GameBootstrapper.Events, pool, respawnDelay);
            GameBootstrapper.Services.Get<GameTicker>().Register(_respawner);

            foreach (SoldierSpawnEntry entry in entries)
            {
                pool.EnsurePool(entry.SoldierPrefab, 1, 1);
                GameObject instance = pool.Get(entry.SoldierPrefab, entry.SpawnPoint.position, entry.SpawnPoint.rotation);
                _respawner.RegisterSpawned(instance, entry);
            }
        }

        private void OnDestroy()
        {
            if (_respawner == null)
            {
                return;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(_respawner);
            }

            _respawner.Dispose();
            _respawner = null;
        }
    }
}
