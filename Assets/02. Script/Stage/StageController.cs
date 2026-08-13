using Character;
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
        private Transform[] leftSpawnPoints;

        [SerializeField]
        private Transform[] rightSpawnPoints;

        [SerializeField]
        private Transform playerTarget;

        [SerializeField]
        private StageSO stageToLoadOnStart;

        [SerializeField]
        private StageCatalogSO catalog;

        [SerializeField]
        private int maxRegressionDistance = 20;

        [SerializeField]
        private StageDifficultyConfigSO difficultyConfig;

        [SerializeField]
        private float tacticUnitSpacing = 1.5f;

        [SerializeField]
        private float tacticRowSpacing = 2f;

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
                StageModeService modeService = null;
                GameBootstrapper.Services?.TryGet(out modeService);
                _progression = new StageProgression(
                    catalog,
                    this,
                    GameBootstrapper.Events,
                    playerTarget,
                    maxRegressionDistance,
                    initialStage,
                    initialHighestStage,
                    modeService);
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
                    return ResolveBreakthroughFrontier(savedStage, save);
                }
            }

            return stageToLoadOnStart;
        }

        /// <summary>
        /// 저장된 현재 스테이지가 최고 기록과 정확히 같으면(반복 모드로 그 스테이지에 머물던 채로
        /// 앱을 종료한 경우) 다음 스테이지로 시작한다. 게임을 새로 켜면 모드가 항상 돌파로
        /// 초기화되는데(SaveData에 모드를 저장하지 않음, 섹션 BL), 이미 클리어한 스테이지에서
        /// 돌파 모드로 다시 시작하는 건 어색하다 - 이미 넘어선 자리이기 때문이다. 현재가 기록보다
        /// 낮으면(죽어서 후퇴한 상태) 손대지 않는다 - 그건 의도된 페널티라 앱 재시작으로 없어지면
        /// 안 된다.
        /// </summary>
        private StageSO ResolveBreakthroughFrontier(StageSO savedStage, SaveData save)
        {
            if (savedStage.Chapter != save.HighestClearedChapter || savedStage.StageNumber != save.HighestClearedStageNumber)
            {
                return savedStage;
            }

            StageSO nextStage = catalog.GetNext(savedStage);
            return nextStage != null ? nextStage : savedStage;
        }

        /// <summary>
        /// 지정한 스테이지를 로드해 몬스터 스폰을 시작한다. 진행 중이던 스테이지가 있다면 먼저 정리한다.
        /// </summary>
        public void LoadStage(StageSO stage)
        {
            EndCurrentStage();

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                pool.EnsurePool(entry.MonsterPrefab, entry.Count, entry.Count);
            }

            if (stage.TacticEntries != null)
            {
                foreach (TacticSpawnEntry tacticEntry in stage.TacticEntries)
                {
                    int pairCount = tacticEntry.TotalUnitCount / 2;
                    pool.EnsurePool(tacticEntry.LeaderPrefab, pairCount, pairCount);
                    pool.EnsurePool(tacticEntry.FollowerPrefab, pairCount, pairCount);

                    if (tacticEntry.AlternateFollowerPrefab != null)
                    {
                        pool.EnsurePool(tacticEntry.AlternateFollowerPrefab, pairCount, pairCount);
                    }
                }
            }

            float statMultiplier = GetStatMultiplier(stage);

            _tracker = new StageProgressTracker(stage, GameBootstrapper.Events);
            _spawner = new MonsterSpawner(
                stage,
                pool,
                topSpawnPoints,
                bottomSpawnPoints,
                leftSpawnPoints,
                rightSpawnPoints,
                playerTarget,
                _tracker,
                statMultiplier,
                tacticUnitSpacing,
                tacticRowSpacing);

            TickerRegistration.Register(_spawner);

            bool isBreakthrough = _progression?.IsBreakthrough ?? true;
            GameBootstrapper.Events?.Publish(new StageChangedEvent(stage.Chapter, stage.StageNumber, isBreakthrough));
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

        /// <summary>
        /// 현재 스테이지를 건드리지 않고 잠깐 "일시정지 + 숨김" 상태로 둔다. 골드 던전처럼 화면을
        /// 잠깐 차지하는 오버레이가 시작될 때 호출한다. StageChangedEvent를 발행하지 않으므로
        /// StageProgression/SaveService/RankService 등 실제 진행도에는 전혀 영향이 없다.
        /// </summary>
        public void PauseForOverlay()
        {
            if (_spawner != null)
            {
                TickerRegistration.Unregister(_spawner);
            }

            _tracker?.SetActiveAll(false);
            _progression?.SetSuppressed(true);
        }

        /// <summary>
        /// PauseForOverlay로 숨겨둔 현재 스테이지를 그대로 되돌린다. 오버레이 도중 Player가
        /// 죽었다면(예: 던전 보스에게 사망) StageChangedEvent가 없어 PlayerReviveOnStageChanged가
        /// 대신 되살려주지 못하므로 여기서 직접 부활시킨다.
        /// </summary>
        /// <summary>
        /// 현재 스테이지를 역대 최고 클리어 스테이지로 옮긴다. StageProgression.JumpToHighestCleared로
        /// 위임한다 - Stage 밖(예: 랭크 승급 가능 자동 반복 전환)에서 StageProgression을 직접
        /// 참조하지 않고 이 창구를 통해서만 호출하게 하기 위함(PauseForOverlay/ResumeAfterOverlay와
        /// 같은 이유).
        /// </summary>
        public void JumpCurrentToHighestCleared()
        {
            _progression?.JumpToHighestCleared();
        }

        public void ResumeAfterOverlay()
        {
            _progression?.SetSuppressed(false);
            _tracker?.SetActiveAll(true);

            if (_spawner != null)
            {
                TickerRegistration.Register(_spawner);
            }

            if (playerTarget != null && playerTarget.TryGetComponent(out Health playerHealth) && playerHealth.IsDead)
            {
                playerHealth.Revive();
            }
        }

        private void EndCurrentStage()
        {
            if (_spawner != null)
            {
                TickerRegistration.Unregister(_spawner);
                _spawner.Dispose();
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
