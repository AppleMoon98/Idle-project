using System.Collections.Generic;
using Behavior;
using Character;
using Combat;
using Dungeon;
using Enhancement;
using Equipment;
using Gacha;
using Inventory;
using Loot;
using Managers;
using Offline;
using Rank;
using Save;
using Services;
using Skill;
using Soldier;
using SoldierEnhancement;
using Stage;
using UI;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 씬 진입점. ServiceLocator/EventBus/GameTicker/PoolManager를 초기화하고
    /// 이후 모든 시스템이 조회할 수 있는 단일 접근점(composition root)을 제공한다.
    /// </summary>
    [RequireComponent(typeof(GameTicker))]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        /// <summary>
        /// 부트스트랩 완료 후 전역에서 조회 가능한 ServiceLocator.
        /// </summary>
        public static ServiceLocator Services { get; private set; }

        /// <summary>
        /// 부트스트랩 완료 후 전역에서 조회 가능한 EventBus.
        /// </summary>
        public static EventBus Events { get; private set; }

        [SerializeField]
        private EnhancementConfigSO[] enhancementConfigs;

        [SerializeField]
        private EnhancementConfigSO[] soldierEnhancementConfigs;

        [SerializeField]
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private StageDifficultyConfigSO stageDifficultyConfig;

        [SerializeField]
        private CharacterStatsSO playerStats;

        [SerializeField]
        private SoldierGradeConfigSO soldierGradeConfig;

        [SerializeField]
        private float maxOfflineHours = 24f;

        [SerializeField]
        private float offlineRewardMultiplier = 0.1f;

        [SerializeField]
        private EquipmentGradeCatalogSO equipmentGradeCatalog;

        [SerializeField]
        private EquipmentCatalogSO equipmentCatalog;

        [SerializeField]
        private EquipmentEnhancementConfigSO equipmentEnhancementConfig;

        [SerializeField]
        private EquipmentStatConfigSO equipmentStatConfig;

        [SerializeField]
        private EquipmentPossessionConfigSO equipmentPossessionConfig;

        [SerializeField]
        private RankCatalogSO rankCatalog;

        [SerializeField]
        private SoldierCatalogSO soldierCatalog;

        [SerializeField]
        private GachaTableSO[] soldierGachaTiers;

        [SerializeField]
        private EquipmentGachaSlotTiers[] equipmentGachaSlots;

        [SerializeField]
        private EquipmentGachaTableSO weaponTicketTable;

        [SerializeField]
        private SkillGachaTableSO[] skillGachaTiers;

        [SerializeField]
        private BehaviorProfileCatalogSO behaviorProfileCatalog;

        [SerializeField]
        private SkillCatalogSO skillCatalog;

        [SerializeField]
        private GameObject damageNumberPrefab;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private UI.CameraPinchZoomUI cameraPinchZoom;

        // GitHub 이슈 #29 - LootDropper가 던전/승급전 오버레이 중에는 일반 스테이지 드롭을
        // 건너뛰기 위해 StageController.IsOverlayActive를 참조해야 한다.
        [SerializeField]
        private StageController stageController;

        /// <summary>
        /// Awake에서 등록한 모든 IManager 인스턴스를 등록 순서대로 모아둔다. 각 서비스의 Shutdown()은
        /// 자기 자신의 이벤트 구독 해제/내부 상태 초기화만 하고 다른 서비스를 참조하지 않으므로
        /// (Shutdown 구현부 확인됨) 순서가 결과에 영향을 주지 않는다 — OnDestroy에서 이 목록을
        /// 한 번에 순회하는 것으로 서비스별 개별 TryGet 블록 반복을 대신한다.
        /// </summary>
        private readonly List<IManager> _managers = new();

        private LootDropper _lootDropper;
        private DamageNumberSpawner _damageNumberSpawner;
        private RareGachaTicketDropService _rareGachaTicketDropService;
        private OfflineProgressService _offlineProgressService;
        private EnhancementService _enhancementService;
        private EquipmentStatService _equipmentStatService;
        private EquipmentPossessionService _equipmentPossessionService;
        private RankService _rankService;
        private SaveData _initialSave;

        private void Awake()
        {
            // 방치형 게임 특성상 화면을 만지지 않고 켜두는 시간이 길어, 기기의 화면 꺼짐/잠금
            // 타이머(SleepTimeout.SystemSetting, 기본값)를 그대로 두면 몇십 초~몇 분 만에 화면이
            // 잠기고 앱이 백그라운드로 밀려 실질적으로 오프라인 처리된다. 앱이 실제로
            // 백그라운드로 전환/종료되는 것(전원 버튼, 앱 전환)까지는 막을 수 없지만 - 그 경우는
            // 기존 OfflineProgressService가 재접속 시 보상으로 커버한다 - 화면을 켜둔 채 아무
            // 조작이 없어서 잠기는 경우만큼은 이걸로 막는다.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Services = new ServiceLocator();
            Events = new EventBus();

            Services.Register(Events);
            Services.Register(GetComponent<GameTicker>());

            var poolManager = new PoolManager();
            poolManager.Initialize();
            Services.Register(poolManager);
            _managers.Add(poolManager);

            var inventoryService = new InventoryService(Events);
            inventoryService.Initialize();
            Services.Register(inventoryService);
            _managers.Add(inventoryService);

            var equippedGearService = new EquippedGearService(Events);
            equippedGearService.Initialize();
            Services.Register(equippedGearService);
            _managers.Add(equippedGearService);

            // SoldierDeploymentService가 슬롯 잠금 해제 수를 물어봐야 해서 RankService를 먼저 만든다.
            _rankService = new RankService(Events, stageCatalog, rankCatalog);
            _rankService.Initialize();
            Services.Register(_rankService);
            _managers.Add(_rankService);

            var soldierRosterService = new SoldierRosterService(Events);
            soldierRosterService.Initialize();
            Services.Register(soldierRosterService);
            _managers.Add(soldierRosterService);

            var soldierDeploymentService = new SoldierDeploymentService(Events, soldierRosterService, _rankService);
            soldierDeploymentService.Initialize();
            Services.Register(soldierDeploymentService);
            _managers.Add(soldierDeploymentService);

            var squadMovementSyncService = new SquadMovementSyncService(Events);
            squadMovementSyncService.Initialize();
            Services.Register(squadMovementSyncService);
            _managers.Add(squadMovementSyncService);

            var monsterSquadMovementSyncService = new MonsterSquadMovementSyncService();
            monsterSquadMovementSyncService.Initialize();
            Services.Register(monsterSquadMovementSyncService);
            _managers.Add(monsterSquadMovementSyncService);

            var squadTacticService = new SquadTacticService(Events);
            squadTacticService.Initialize();
            Services.Register(squadTacticService);
            _managers.Add(squadTacticService);

            var squadShieldWallCoordinator = new SquadShieldWallCoordinator(Events, squadTacticService, squadMovementSyncService);
            squadShieldWallCoordinator.Initialize();
            Services.Register(squadShieldWallCoordinator);
            _managers.Add(squadShieldWallCoordinator);

            var skillService = new SkillService(Events);
            skillService.Initialize();
            Services.Register(skillService);
            _managers.Add(skillService);

            var skillLoadoutService = new SkillLoadoutService(Events, skillService);
            skillLoadoutService.Initialize();
            Services.Register(skillLoadoutService);
            _managers.Add(skillLoadoutService);

            var saveService = new SaveService(
                Events,
                inventoryService,
                equippedGearService,
                equipmentCatalog,
                equipmentEnhancementConfig,
                soldierRosterService,
                soldierCatalog,
                soldierDeploymentService,
                behaviorProfileCatalog,
                skillService,
                skillCatalog,
                skillLoadoutService,
                squadTacticService);
            saveService.Initialize();
            Services.Register(saveService);
            _managers.Add(saveService);

            SaveData save = saveService.Load();
            _initialSave = save;
            saveService.RestoreInventory(save);
            saveService.RestoreSoldierRoster(save);
            saveService.RestoreSkills(save);
            saveService.RestoreSkillCounts(save);
            saveService.RestoreSkillLoadout(save);
            saveService.RestoreSkillEnabled(save);
            saveService.RestoreSquadTactics(save);

            // OfflineProgressService.CalculateAndApply()(Start()에서 제일 먼저 실행됨)보다 반드시
            // 먼저 랭크를 맞춰둬야 한다 — 그렇지 않으면 아직 시골 소년 상태인 RankService가 오프라인
            // 진행의 HighestStageClearedEvent를 받아 이미 딴 랭크까지 진짜 승급으로 재계산해버린다.
            _rankService.SeedRank(save.RankIndex);

            var currencyService = new CurrencyService(Events, save.Gold);
            currencyService.Initialize();
            Services.Register(currencyService);
            _managers.Add(currencyService);

            var enhancementStoneService = new EnhancementStoneService(Events, save.EnhancementStones);
            enhancementStoneService.Initialize();
            Services.Register(enhancementStoneService);
            _managers.Add(enhancementStoneService);

            _enhancementService = new EnhancementService(Events, currencyService, enhancementConfigs);
            _enhancementService.Initialize();
            Services.Register(_enhancementService);
            _managers.Add(_enhancementService);

            var soldierEnhancementService = new SoldierEnhancementService(Events, currencyService, soldierEnhancementConfigs);
            soldierEnhancementService.Initialize();
            Services.Register(soldierEnhancementService);
            _managers.Add(soldierEnhancementService);

            // Player의 RestoreLevel과 달리 여기서(Awake, Start가 아니라) 바로 복원한다 — 병사는
            // SoldierStatReceiver가 스폰 시점(OnEnable)에 현재 레벨을 직접 조회하는 방식이라, 다른
            // 구독자의 OnEnable을 기다릴 필요 없이 어떤 병사보다도 먼저 세팅되기만 하면 된다.
            LogIfClamped("병사", EnhancementStatType.AttackPower, soldierEnhancementService.RestoreLevel(EnhancementStatType.AttackPower, save.SoldierAttackPowerLevel));
            LogIfClamped("병사", EnhancementStatType.MaxHealth, soldierEnhancementService.RestoreLevel(EnhancementStatType.MaxHealth, save.SoldierMaxHealthLevel));
            LogIfClamped("병사", EnhancementStatType.AttackSpeed, soldierEnhancementService.RestoreLevel(EnhancementStatType.AttackSpeed, save.SoldierAttackSpeedLevel));
            LogIfClamped("병사", EnhancementStatType.MoveSpeed, soldierEnhancementService.RestoreLevel(EnhancementStatType.MoveSpeed, save.SoldierMoveSpeedLevel));
            LogIfClamped("병사", EnhancementStatType.CriticalChance, soldierEnhancementService.RestoreLevel(EnhancementStatType.CriticalChance, save.SoldierCriticalChanceLevel));
            LogIfClamped("병사", EnhancementStatType.CriticalDamage, soldierEnhancementService.RestoreLevel(EnhancementStatType.CriticalDamage, save.SoldierCriticalDamageLevel));

            var equipmentFusionService = new EquipmentFusionService(Events, inventoryService, equipmentGradeCatalog, equipmentCatalog);
            equipmentFusionService.Initialize();
            Services.Register(equipmentFusionService);
            _managers.Add(equipmentFusionService);

            var equipmentEnhancementService = new EquipmentEnhancementService(inventoryService, enhancementStoneService, equipmentEnhancementConfig, equipmentGradeCatalog);
            equipmentEnhancementService.Initialize();
            Services.Register(equipmentEnhancementService);
            _managers.Add(equipmentEnhancementService);

            _equipmentStatService = new EquipmentStatService(
                Events,
                equippedGearService,
                equipmentGradeCatalog,
                equipmentEnhancementConfig,
                equipmentStatConfig);
            _equipmentStatService.Initialize();
            Services.Register(_equipmentStatService);
            _managers.Add(_equipmentStatService);

            _equipmentPossessionService = new EquipmentPossessionService(
                Events,
                inventoryService,
                equipmentGradeCatalog,
                equipmentPossessionConfig);
            _equipmentPossessionService.Initialize();
            Services.Register(_equipmentPossessionService);
            _managers.Add(_equipmentPossessionService);

            var soldierTicketService = new SoldierTicketService(Events, save.SoldierTicketCount);
            soldierTicketService.Initialize();
            Services.Register(soldierTicketService);
            _managers.Add(soldierTicketService);

            var gachaService = new GachaService(Events, soldierTicketService, currencyService, soldierRosterService, soldierGachaTiers);
            gachaService.Initialize();
            Services.Register(gachaService);
            _managers.Add(gachaService);

            var equipmentGachaTicketService = new EquipmentGachaTicketService(Events, save.EquipmentGachaTicketCount);
            equipmentGachaTicketService.Initialize();
            Services.Register(equipmentGachaTicketService);
            _managers.Add(equipmentGachaTicketService);

            var equipmentGachaService = new EquipmentGachaService(Events, currencyService, equipmentGachaSlots, equipmentGachaTicketService, weaponTicketTable);
            equipmentGachaService.Initialize();
            Services.Register(equipmentGachaService);
            _managers.Add(equipmentGachaService);

            var skillScrollService = new SkillScrollService(Events, save.SkillScrollCount);
            skillScrollService.Initialize();
            Services.Register(skillScrollService);
            _managers.Add(skillScrollService);

            var bossTokenService = new BossTokenService(Events, save.BossTokenCount);
            bossTokenService.Initialize();
            Services.Register(bossTokenService);
            _managers.Add(bossTokenService);

            var skillGachaService = new SkillGachaService(Events, skillScrollService, currencyService, skillService, skillGachaTiers);
            skillGachaService.Initialize();
            Services.Register(skillGachaService);
            _managers.Add(skillGachaService);

            // GachaService/SkillGachaService는 SoldierTicketCount/SkillScrollCount 등 save.Load()
            // 결과에 의존해 SaveService보다 늦게 생성되므로(위 참고), 골드 뽑기 누적 횟수 복원도
            // 두 서비스가 다 생긴 지금 시점에 한다(SaveService.RestoreGachaPullCounts 참고).
            saveService.RestoreGachaPullCounts(save);

            var soldierTargetRegistry = new SoldierTargetRegistry();
            soldierTargetRegistry.Initialize();
            Services.Register(soldierTargetRegistry);
            _managers.Add(soldierTargetRegistry);

            var playerControlModeService = new PlayerControlModeService(Events);
            playerControlModeService.Initialize();
            Services.Register(playerControlModeService);
            _managers.Add(playerControlModeService);

            var stageModeService = new StageModeService(Events);
            stageModeService.Initialize();
            Services.Register(stageModeService);
            _managers.Add(stageModeService);

            var cameraShakeService = new CameraShakeService(Events);
            cameraShakeService.Initialize();
            Services.Register(cameraShakeService);
            _managers.Add(cameraShakeService);

            var cameraFollowService = new CameraFollowService(playerTransform, cameraPinchZoom);
            cameraFollowService.Initialize();
            Services.Register(cameraFollowService);
            _managers.Add(cameraFollowService);

            var backNavigationService = new BackNavigationService();
            backNavigationService.Initialize();
            Services.Register(backNavigationService);
            _managers.Add(backNavigationService);

            var offlineCombatPowerCalculator = new OfflineCombatPowerCalculator(
                playerStats,
                _enhancementService,
                _equipmentStatService,
                _equipmentPossessionService,
                soldierEnhancementService,
                soldierDeploymentService,
                soldierGradeConfig,
                _rankService,
                skillService,
                skillLoadoutService);

            var offlineStageSimulator = new OfflineStageSimulator(stageCatalog, stageDifficultyConfig, offlineRewardMultiplier);

            _offlineProgressService = new OfflineProgressService(
                Events,
                saveService,
                offlineCombatPowerCalculator,
                offlineStageSimulator,
                maxOfflineHours * 3600f);

            _lootDropper = new LootDropper(Events, stageCatalog, stageDifficultyConfig, stageController);
            _damageNumberSpawner = new DamageNumberSpawner(Events, poolManager, damageNumberPrefab);
            _rareGachaTicketDropService = new RareGachaTicketDropService(Events, stageCatalog, equipmentGachaTicketService, soldierTicketService, skillScrollService);
        }

        private void Start()
        {
            // 다른 오브젝트들의 OnEnable(이벤트 구독 포함)이 모두 끝난 뒤(Start 시점)에 호출해야
            // StatEnhancedEvent/OfflineProgressCalculatedEvent를 구독하는 쪽이 이벤트를 놓치지 않는다.
            //
            // CaptureBudget()을 반드시 가장 먼저 호출해야 한다 — 아래 RestoreLevel 호출들이
            // 발행하는 StatEnhancedEvent/RankChangedEvent를 SaveService가 구독해 즉시 Save()를
            // 호출하는데, Save()는 LastActiveUnixTime을 항상 "지금"으로 덮어쓴다. 경과 시간 확정이
            // 그 뒤에 실행되면 이미 덮어써진 시각을 읽어 경과 시간이 0이 되어버린다(실제로 발생했던
            // 버그). 실제 보상 계산(ApplyCapturedReward)은 반대로 이 메서드들이 전부 끝난 뒤(Start()
            // 맨 마지막)에 호출한다 — "세이브 복원이 완료된 시점의 유효 전투력 스냅샷"을 쓰기 위해서다.
            _offlineProgressService?.CaptureBudget();

            LogIfClamped("플레이어", EnhancementStatType.AttackPower, _enhancementService?.RestoreLevel(EnhancementStatType.AttackPower, _initialSave.AttackPowerLevel));
            LogIfClamped("플레이어", EnhancementStatType.MaxHealth, _enhancementService?.RestoreLevel(EnhancementStatType.MaxHealth, _initialSave.MaxHealthLevel));
            LogIfClamped("플레이어", EnhancementStatType.AttackSpeed, _enhancementService?.RestoreLevel(EnhancementStatType.AttackSpeed, _initialSave.AttackSpeedLevel));
            LogIfClamped("플레이어", EnhancementStatType.MoveSpeed, _enhancementService?.RestoreLevel(EnhancementStatType.MoveSpeed, _initialSave.MoveSpeedLevel));
            LogIfClamped("플레이어", EnhancementStatType.CriticalChance, _enhancementService?.RestoreLevel(EnhancementStatType.CriticalChance, _initialSave.CriticalChanceLevel));
            LogIfClamped("플레이어", EnhancementStatType.CriticalDamage, _enhancementService?.RestoreLevel(EnhancementStatType.CriticalDamage, _initialSave.CriticalDamageLevel));

            // RestoreInventory()는 세이브 시딩일 뿐 이벤트를 발행하지 않으므로, 복원된 장착 상태를
            // EquipmentStatReceiver가 놓치지 않도록 여기서 한 번 직접 재계산/발행한다.
            _equipmentStatService?.RecomputeAndPublish();
            _equipmentPossessionService?.RecomputeAndPublish();

            // SeedHighestCleared를 RestoreLevel보다 먼저 호출해야 한다 - RestoreLevel이 발행하는
            // RankChangedEvent를 RankUpAvailableTextUI가 받아 IsNextRankAvailable()을 재계산하는데,
            // 그 시점에 _highestClearedIndex가 아직 시딩 전(-1)이면 이미 요구 스테이지를 넘어선
            // 세이브도 승급 가능 버튼이 뜨지 않는다(새로 스테이지를 클리어해야만 다시 체크됨).
            _rankService?.SeedHighestCleared(_initialSave.HighestClearedChapter, _initialSave.HighestClearedStageNumber);
            _rareGachaTicketDropService?.SeedHighestCleared(_initialSave.HighestClearedChapter, _initialSave.HighestClearedStageNumber);
            _rankService?.RestoreLevel(_initialSave.RankIndex);

            // 위의 모든 RestoreLevel/RecomputeAndPublish가 끝나 세이브 복원이 완전히 마무리된 시점 —
            // CaptureBudget()이 미리 확정해둔 경과 시간으로, 지금 이 순간의 유효 전투력 스냅샷을
            // 사용해 실제 오프라인 보상을 계산/적용한다(OfflineProgressService 클래스 doc 참고).
            _offlineProgressService?.ApplyCapturedReward();
        }

        /// <summary>
        /// GitHub 이슈 #50 - Enhancement.EnhancementService/SoldierEnhancement.SoldierEnhancementService.
        /// RestoreLevel이 저장된 레벨을 설정 최대치로 보정했을 때만(정상 복원은 로그하지 않음)
        /// 경고를 남긴다. label은 "플레이어"/"병사"처럼 어느 강화 트랙인지 구분하는 표시.
        /// </summary>
        private static void LogIfClamped(string label, EnhancementStatType statType, LevelRestoreOutcome? outcome)
        {
            if (outcome == LevelRestoreOutcome.ClampedToMax)
            {
                Debug.LogWarning($"[GameBootstrapper] {label} {statType} 강화 레벨이 저장된 값에서 설정 최대치로 보정됨 - 손상된 저장 데이터이거나 설정 최대 레벨이 낮아졌을 수 있음(GitHub 이슈 #50).");
            }
        }

        /// <summary>
        /// GitHub 이슈 #28 - saveService.Save()를 직접 호출하지 않는다. Save()는 캐시된 스냅샷
        /// 문자열이 최신인지 확인하지 않으므로, 장비/병사/스킬 컬렉션이 바뀐 바로 그 프레임에
        /// Tick이 아직 한 번도 안 돈 채 pause/quit이 오면 낡은 캐시가 영구 저장된다.
        /// FlushPendingChanges()는 더티 스냅샷을 먼저 최신화한 뒤에야 Save()로 넘어간다.
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            // GitHub 이슈 #49 - FlushPendingChanges()는 더티 상태가 아니면 LastActiveUnixTime을
            // 갱신하지 않는다. pause/quit은 FlushForApplicationLifecycle()을 써서 변경 사항이
            // 없어도 마지막 활동 시각만은 반드시 갱신되도록 한다.
            if (pauseStatus && Services != null && Services.TryGet(out SaveService saveService))
            {
                saveService.FlushForApplicationLifecycle();
            }
        }

        private void OnApplicationQuit()
        {
            if (Services != null && Services.TryGet(out SaveService saveService))
            {
                saveService.FlushForApplicationLifecycle();
            }
        }

        private void OnDestroy()
        {
            _lootDropper?.Dispose();
            _lootDropper = null;

            _damageNumberSpawner?.Dispose();
            _damageNumberSpawner = null;

            _rareGachaTicketDropService?.Dispose();
            _rareGachaTicketDropService = null;

            for (int i = 0; i < _managers.Count; i++)
            {
                _managers[i].Shutdown();
            }

            _managers.Clear();

            Services?.Clear();
            Events?.Clear();

            Services = null;
            Events = null;
        }
    }
}
