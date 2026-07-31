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
        private Transform[] topSpawnPoints;

        [SerializeField]
        private Transform[] bottomSpawnPoints;

        [SerializeField]
        private Transform playerTarget;

        [SerializeField]
        private float playerNearTopViewportThreshold = 0.5f;

        [SerializeField]
        private StageSO stageToLoadOnStart;

        [SerializeField]
        private StageCatalogSO catalog;

        [SerializeField]
        private int maxRegressionDistance = 20;

        [SerializeField]
        private StageDifficultyConfigSO difficultyConfig;

        private MonsterSpawner _spawner;
        private StageProgressTracker _tracker;
        private StageProgression _progression;

        private void Start()
        {
            SaveData save = LoadSave();
            StageSO initialStage = ResolveInitialStage(save);

            if (catalog != null)
            {
                StageSO initialHighestStage = catalog.Find(save.HighestClearedChapter, save.HighestClearedStageNumber);
                _progression = new StageProgression(
                    catalog,
                    this,
                    GameBootstrapper.Events,
                    playerTarget,
                    maxRegressionDistance,
                    initialStage,
                    initialHighestStage);
            }

            if (initialStage != null)
            {
                LoadStage(initialStage);
            }
        }

        private static SaveData LoadSave()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SaveService saveService))
            {
                return saveService.Load();
            }

            return default;
        }

        /// <summary>
        /// 저장된 진행(현재 스테이지)이 있으면 그것을, 없으면(최초 실행) stageToLoadOnStart를 반환한다.
        /// </summary>
        private StageSO ResolveInitialStage(SaveData save)
        {
            if (catalog != null && save.LastActiveUnixTime > 0)
            {
                StageSO savedStage = catalog.Find(save.Chapter, save.StageNumber);

                if (savedStage != null)
                {
                    return savedStage;
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

            float statMultiplier = GetStatMultiplier(stage);

            _tracker = new StageProgressTracker(stage, GameBootstrapper.Events);
            _spawner = new MonsterSpawner(stage, pool, topSpawnPoints, bottomSpawnPoints, playerTarget, _tracker, playerNearTopViewportThreshold, statMultiplier);

            GameBootstrapper.Services.Get<GameTicker>().Register(_spawner);

            GameBootstrapper.Events?.Publish(new StageChangedEvent(stage.Chapter, stage.StageNumber));
        }

        /// <summary>
        /// 카탈로그 상 stage의 인덱스로 난이도 배율을 계산한다. 카탈로그/설정이 없으면 1배(배율 없음).
        /// </summary>
        private float GetStatMultiplier(StageSO stage)
        {
            if (catalog == null || difficultyConfig == null)
            {
                return 1f;
            }

            int stageIndex = catalog.IndexOf(stage);
            return difficultyConfig.GetMultiplier(stageIndex);
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

            if (_tracker != null)
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
                {
                    _tracker.ReleaseRemaining(pool);
                }

                _tracker.Dispose();
                _tracker = null;
            }
        }

        private void OnDestroy()
        {
            EndCurrentStage();

            _progression?.Dispose();
            _progression = null;
        }
    }
}
