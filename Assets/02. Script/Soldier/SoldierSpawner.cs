using Core;
using Managers;
using Rank;
using Rank.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 엔트리별로 지정된 Soldier 프리팹을 지정된 스폰 지점에 하나씩 배치한다. requiredRank가
    /// 설정돼 있으면 현재 랭크가 그 이상이 될 때까지 스폰을 미루고, RankChangedEvent를 구독해
    /// 승급 즉시 스폰한다(null이면 조건 없이 항상 활성). 스폰한 병사가 사망하면 SoldierRespawner를
    /// 통해 respawnDelay 후 같은 자리에서 다시 소환한다.
    /// </summary>
    public sealed class SoldierSpawner : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawnEntry[] entries;

        [SerializeField]
        private float respawnDelay = 5f;

        [SerializeField]
        private RankSO requiredRank;

        private SoldierRespawner _respawner;
        private PoolManager _pool;
        private bool _spawned;

        private void Start()
        {
            if (!GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            _pool = pool;
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);

            TrySpawn();
        }

        private void OnDestroy()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);

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

        private void OnRankChanged(RankChangedEvent evt)
        {
            TrySpawn();
        }

        private void TrySpawn()
        {
            if (_spawned)
            {
                return;
            }

            if (!GameBootstrapper.Services.TryGet(out RankService rankService) || !rankService.IsAtLeast(requiredRank))
            {
                return;
            }

            _spawned = true;

            _respawner = new SoldierRespawner(GameBootstrapper.Events, _pool, respawnDelay);
            GameBootstrapper.Services.Get<GameTicker>().Register(_respawner);

            foreach (SoldierSpawnEntry entry in entries)
            {
                _pool.EnsurePool(entry.SoldierPrefab, 1, 1);
                GameObject instance = _pool.Get(entry.SoldierPrefab, entry.SpawnPoint.position, entry.SpawnPoint.rotation);
                _respawner.RegisterSpawned(instance, entry);
            }
        }
    }
}
