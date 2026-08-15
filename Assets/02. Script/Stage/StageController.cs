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
        private StagePositionResetter positionResetter;

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
        /// overlayLabel을 주면(예: "골드 던전 1층") StageOverlayLabelChangedEvent로 발행해
        /// UI.StageInfoUI가 상단 스테이지 정보 텍스트를 그 라벨로 잠깐 바꾸도록 한다 - 랭크
        /// 승급전/War 클라이맥스처럼 이 인자를 생략하는 호출부는 기존과 동일하게 아무 표시 변화가
        /// 없다.
        /// </summary>
        public void PauseForOverlay(string overlayLabel = null)
        {
            IsOverlayActive = true;

            if (_spawner != null)
            {
                TickerRegistration.Unregister(_spawner);
            }

            _tracker?.SetActiveAll(false);
            _progression?.SetSuppressed(true);
            positionResetter?.ResetPositions();

            if (overlayLabel != null)
            {
                GameBootstrapper.Events?.Publish(new Stage.Events.StageOverlayLabelChangedEvent(overlayLabel));
            }
        }

        /// <summary>
        /// 골드/강화석/스킬/병사 구출 던전, 랭크 승급전 등 PauseForOverlay를 쓰는 오버레이 중
        /// 하나라도 현재 진행 중인지 여부. 각 오버레이의 진입 메서드(Enter 등)는 이 값을 자기
        /// 자신의 _isActive와 함께 확인해, 이미 다른 오버레이가 켜져 있으면 중복 진입을 막아야
        /// 한다 - 이게 없으면(예: 골드 던전 진행 중에 강화석 던전 팝업의 입장 버튼을 누르는 것)
        /// PauseForOverlay/ResumeAfterOverlay가 서로 안 맞물려 호출돼(참조 카운트가 아니라 단순
        /// on/off라) 어느 한쪽이 먼저 끝나면 다른 쪽이 아직 진행 중인데도 스테이지가 재개되는 등
        /// 상태가 완전히 꼬인다.
        /// </summary>
        public bool IsOverlayActive { get; private set; }

        /// <summary>
        /// PauseForOverlay로 숨겨둔 현재 스테이지를 그대로 되돌린다. 오버레이 도중 Player가
        /// 죽었다면(예: 던전 보스에게 사망) StageChangedEvent가 없어 PlayerReviveOnStageChanged가
        /// 대신 되살려주지 못하므로 여기서 직접 부활시킨다.
        /// </summary>
        /// <summary>
        /// 좌측 스폰 지점 중 index를 순환 인덱싱해 반환한다. MonsterSpawner가 내부적으로 쓰는 것과
        /// 같은 배열을 그대로 재사용한다 - 병사 습격 전술(Soldier.SquadRaidCoordinator)처럼 Stage
        /// 밖에서도 이 화면 가장자리 좌표가 필요한 경우를 위한 공개 창구. 배열이 비어있으면 null.
        /// </summary>
        public Transform GetLeftSpawnPoint(int index)
        {
            return GetCyclicSpawnPoint(leftSpawnPoints, index);
        }

        /// <summary>
        /// GetLeftSpawnPoint와 동일하되 우측 스폰 지점 배열을 순환 인덱싱한다.
        /// </summary>
        public Transform GetRightSpawnPoint(int index)
        {
            return GetCyclicSpawnPoint(rightSpawnPoints, index);
        }

        /// <summary>
        /// GetLeftSpawnPoint와 동일하되 상단 스폰 지점 배열을 순환 인덱싱한다.
        /// </summary>
        public Transform GetTopSpawnPoint(int index)
        {
            return GetCyclicSpawnPoint(topSpawnPoints, index);
        }

        private static Transform GetCyclicSpawnPoint(Transform[] points, int index)
        {
            if (points == null || points.Length == 0)
            {
                return null;
            }

            int wrapped = ((index % points.Length) + points.Length) % points.Length;
            return points[wrapped];
        }

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

        /// <summary>
        /// 현재 스테이지를 역대 최고 클리어 스테이지의 다음(돌파 프론티어)으로 옮긴다.
        /// StageProgression.JumpToBreakthroughFrontier로 위임한다(반복 모드에서 돌파 모드로
        /// 되돌아갈 때 StageModeToggleUI가 호출하는 창구, JumpCurrentToHighestCleared와 같은 이유
        /// 로 StageProgression을 외부에 직접 노출하지 않는다).
        /// </summary>
        public void JumpCurrentToBreakthroughFrontier()
        {
            _progression?.JumpToBreakthroughFrontier();
        }

        /// <summary>
        /// 현재 스테이지를 stage로 직접 옮긴다(반복 모드 스테이지 선택 팝업 전용 창구,
        /// JumpCurrentToHighestCleared와 같은 이유로 StageProgression을 외부에 직접 노출하지
        /// 않는다). 아직 클리어하지 않은 스테이지면 아무 일도 하지 않고 false.
        /// </summary>
        public bool JumpCurrentToStage(StageSO stage)
        {
            return _progression != null && _progression.JumpTo(stage);
        }

        /// <summary>
        /// 역대 최고 클리어 스테이지를 포함해 최대 count개(최고 기록부터 내림차순)의 이미 클리어한
        /// 스테이지 목록을 반환한다. 반복 모드 스테이지 선택 팝업이 고를 수 있는 후보 목록으로 쓴다.
        /// </summary>
        public StageSO[] GetRepeatableStages(int count)
        {
            if (_progression == null || catalog == null)
            {
                return System.Array.Empty<StageSO>();
            }

            int highest = _progression.HighestClearedIndex;
            int floor = Mathf.Max(0, highest - (count - 1));
            var result = new System.Collections.Generic.List<StageSO>();

            for (int i = highest; i >= floor; i--)
            {
                StageSO stage = catalog.GetAt(i);

                if (stage != null)
                {
                    result.Add(stage);
                }
            }

            return result.ToArray();
        }

        public void ResumeAfterOverlay()
        {
            IsOverlayActive = false;

            _progression?.SetSuppressed(false);
            _tracker?.SetActiveAll(true);
            positionResetter?.ResetPositions();
            positionResetter?.ResetHealth();
            GameBootstrapper.Events?.Publish(new Stage.Events.StageOverlayLabelChangedEvent(null));

            if (_spawner != null)
            {
                TickerRegistration.Register(_spawner);
            }
        }

        /// <summary>
        /// 오버레이(던전 등) 안에서 재도전할 때 호출한다. IsOverlayActive/스포너/트래커/진행도는
        /// 전혀 건드리지 않는다 — 오버레이 자체는 아직 끝나지 않았기 때문이다(재도전 대기 상태).
        /// 플레이어와 병사의 체력만 이번 시도를 시작할 때처럼 되돌린다
        /// (StagePositionResetter.ResetHealth, ResumeAfterOverlay가 클리어/나가기 시점에 쓰는 것과
        /// 동일한 로직) — 던전 각 세션 컨트롤러의 Retry()가 호출한다.
        /// </summary>
        public void ResetCombatantsForRetry()
        {
            positionResetter?.ResetHealth();
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
