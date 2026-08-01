using Core;
using Managers;
using Rank;
using Rank.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 배치 슬롯마다 SoldierDeploymentService에 배정된 로스터 유닛을 스폰한다. requiredRank가
    /// 설정돼 있으면 현재 랭크가 그 이상이 될 때까지 스폰을 미루고, RankChangedEvent를 구독해
    /// 승급 즉시 스폰한다(null이면 조건 없이 항상 활성). 슬롯에 배정이 없으면(로스터가 비었거나
    /// 아직 아무도 배치하지 않았으면) 그 슬롯은 스폰하지 않는다. 스폰한 병사가 사망하면
    /// SoldierRespawner를 통해 respawnDelay 후 그 시점의 배정을 다시 확인해 재소환한다.
    /// </summary>
    public sealed class SoldierSpawner : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawnSlot[] slots;

        [SerializeField]
        private float respawnDelay = 5f;

        [SerializeField]
        private RankSO requiredRank;

        private SoldierRespawner _respawner;
        private PoolManager _pool;
        private SoldierDeploymentService _deployment;
        private bool _spawned;

        private void Start()
        {
            if (!GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            _pool = pool;
            GameBootstrapper.Services.TryGet(out _deployment);
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

            _respawner = new SoldierRespawner(GameBootstrapper.Events, _pool, _deployment, respawnDelay);
            GameBootstrapper.Services.Get<GameTicker>().Register(_respawner);

            foreach (SoldierSpawnSlot slot in slots)
            {
                SpawnSlot(slot);
            }
        }

        private void SpawnSlot(SoldierSpawnSlot slot)
        {
            if (_deployment == null || !_deployment.TryGetAssigned(slot.SlotIndex, out OwnedSoldier owned) || owned.Definition.Prefab == null)
            {
                return;
            }

            _pool.EnsurePool(owned.Definition.Prefab, 1, 1);
            GameObject instance = _pool.Get(owned.Definition.Prefab, slot.SpawnPoint.position, slot.SpawnPoint.rotation);

            if (instance.TryGetComponent(out SoldierBehaviorController controller))
            {
                controller.Initialize(owned.InstanceId, slot.SpawnPoint);
            }

            _respawner.RegisterSpawned(instance, slot);
        }
    }
}
