using Core;
using Managers;
using Save;
using Stage.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 하나의 스테이지 실행을 조립하는 진입점.
    /// MonsterSpawner/StageProgressTracker를 생성하고 생명주기를 관리한다.
    /// </summary>
    public sealed class StageController : MonoBehaviour
    {
        [SerializeField]
        private Transform[] spawnPoints;

        [SerializeField]
        private Transform playerTarget;

        [SerializeField]
        private StageSO stageToLoadOnStart;

        [SerializeField]
        private StageCatalogSO catalog;

        private MonsterSpawner _spawner;
        private StageProgressTracker _tracker;
        private StageProgression _progression;

        private void Start()
        {
            if (catalog != null)
            {
                _progression = new StageProgression(catalog, this, GameBootstrapper.Events);
            }

            StageSO initialStage = ResolveInitialStage();

            if (initialStage != null)
            {
                LoadStage(initialStage);
            }
        }

        /// <summary>
        /// 저장된 진행(스테이지)이 있으면 그것을, 없으면(최초 실행) stageToLoadOnStart를 반환한다.
        /// </summary>
        private StageSO ResolveInitialStage()
        {
            if (catalog != null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SaveService saveService))
            {
                SaveData save = saveService.Load();

                if (save.LastActiveUnixTime > 0)
                {
                    StageSO savedStage = catalog.Find(save.Chapter, save.StageNumber);

                    if (savedStage != null)
                    {
                        return savedStage;
                    }
                }
            }

            return stageToLoadOnStart;
        }

        /// <summary>
        /// 지정한 스테이지를 로드해 몬스터 스폰을 시작한다. 진행 중이던 스테이지가 있다면 먼저 정리한다.
        /// </summary>
        public void LoadStage(StageSO stage)
        {
            EndCurrentStage();

            PoolManager pool = GameBootstrapper.Services.Get<PoolManager>();

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                pool.EnsurePool(entry.MonsterPrefab, entry.Count, entry.Count);
            }

            _tracker = new StageProgressTracker(stage, GameBootstrapper.Events);
            _spawner = new MonsterSpawner(stage, pool, spawnPoints, playerTarget, _tracker);

            GameBootstrapper.Services.Get<GameTicker>().Register(_spawner);

            GameBootstrapper.Events?.Publish(new StageChangedEvent(stage.Chapter, stage.StageNumber));
        }

        private void EndCurrentStage()
        {
            if (_spawner != null)
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
                {
                    ticker.Unregister(_spawner);
                }

                _spawner = null;
            }

            _tracker?.Dispose();
            _tracker = null;
        }

        private void OnDestroy()
        {
            EndCurrentStage();

            _progression?.Dispose();
            _progression = null;
        }
    }
}
