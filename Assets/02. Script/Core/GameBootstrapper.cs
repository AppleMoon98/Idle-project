using System.Collections.Generic;
using Behavior;
using Character;
using Combat;
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
using SoldierEquipment;
using Stage;
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
        private CharacterStatsSO soldierStats;

        [SerializeField]
        private int soldierCount;

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
        private RankCatalogSO rankCatalog;

        [SerializeField]
        private SoldierCatalogSO soldierCatalog;

        [SerializeField]
        private GachaTableSO[] soldierGachaTiers;

        [SerializeField]
        private EquipmentGachaSlotTiers[] equipmentGachaSlots;

        [SerializeField]
        private SkillGachaTableSO[] skillGachaTiers;

        [SerializeField]
        private SoldierEquipmentCatalogSO soldierEquipmentCatalog;

        [SerializeField]
        private BehaviorProfileCatalogSO behaviorProfileCatalog;

        [SerializeField]
        private SkillCatalogSO skillCatalog;

        [SerializeField]
        private GameObject damageNumberPrefab;

        /// <summary>
        /// Awake에서 등록한 모든 IManager 인스턴스를 등록 순서대로 모아둔다. 각 서비스의 Shutdown()은
        /// 자기 자신의 이벤트 구독 해제/내부 상태 초기화만 하고 다른 서비스를 참조하지 않으므로
        /// (Shutdown 구현부 확인됨) 순서가 결과에 영향을 주지 않는다 — OnDestroy에서 이 목록을
        /// 한 번에 순회하는 것으로 서비스별 개별 TryGet 블록 반복을 대신한다.
        /// </summary>
        private readonly List<IManager> _managers = new();

        private LootDropper _lootDropper;
        private DamageNumberSpawner _damageNumberSpawner;
        private OfflineProgressService _offlineProgressService;
        private EnhancementService _enhancementService;
        private EquipmentStatService _equipmentStatService;
        private RankService _rankService;
        private SaveData _initialSave;

        private void Awake()
        {
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

            var soldierEquipmentInventoryService = new SoldierEquipmentInventoryService(Events);
            soldierEquipmentInventoryService.Initialize();
            Services.Register(soldierEquipmentInventoryService);
            _managers.Add(soldierEquipmentInventoryService);

            var soldierEquippedGearService = new SoldierEquippedGearService(Events, soldierEquipmentInventoryService);
            soldierEquippedGearService.Initialize();
            Services.Register(soldierEquippedGearService);
            _managers.Add(soldierEquippedGearService);

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
                soldierRosterService,
                soldierCatalog,
                soldierDeploymentService,
                behaviorProfileCatalog,
                soldierEquipmentInventoryService,
                soldierEquippedGearService,
                soldierEquipmentCatalog,
                skillService,
                skillCatalog,
                skillLoadoutService);
            saveService.Initialize();
            Services.Register(saveService);
            _managers.Add(saveService);

            SaveData save = saveService.Load();
            _initialSave = save;
            saveService.RestoreInventory(save);
            saveService.RestoreSoldierRoster(save);
            saveService.RestoreSoldierEquipment(save);
            saveService.RestoreSkills(save);
            saveService.RestoreSkillLoadout(save);
            saveService.RestoreSkillEnabled(save);

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
            soldierEnhancementService.RestoreLevel(EnhancementStatType.AttackPower, save.SoldierAttackPowerLevel);
            soldierEnhancementService.RestoreLevel(EnhancementStatType.MaxHealth, save.SoldierMaxHealthLevel);
            soldierEnhancementService.RestoreLevel(EnhancementStatType.AttackSpeed, save.SoldierAttackSpeedLevel);
            soldierEnhancementService.RestoreLevel(EnhancementStatType.MoveSpeed, save.SoldierMoveSpeedLevel);
            soldierEnhancementService.RestoreLevel(EnhancementStatType.CriticalChance, save.SoldierCriticalChanceLevel);
            soldierEnhancementService.RestoreLevel(EnhancementStatType.CriticalDamage, save.SoldierCriticalDamageLevel);

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

            var soldierTicketService = new SoldierTicketService(Events, save.SoldierTicketCount);
            soldierTicketService.Initialize();
            Services.Register(soldierTicketService);
            _managers.Add(soldierTicketService);

            var gachaService = new GachaService(Events, soldierTicketService, soldierRosterService, soldierGachaTiers);
            gachaService.Initialize();
            Services.Register(gachaService);
            _managers.Add(gachaService);

            var equipmentGachaService = new EquipmentGachaService(Events, currencyService, equipmentGachaSlots);
            equipmentGachaService.Initialize();
            Services.Register(equipmentGachaService);
            _managers.Add(equipmentGachaService);

            var skillScrollService = new SkillScrollService(Events, save.SkillScrollCount);
            skillScrollService.Initialize();
            Services.Register(skillScrollService);
            _managers.Add(skillScrollService);

            var skillGachaService = new SkillGachaService(Events, skillScrollService, skillService, skillGachaTiers);
            skillGachaService.Initialize();
            Services.Register(skillGachaService);
            _managers.Add(skillGachaService);

            var soldierTargetRegistry = new SoldierTargetRegistry();
            soldierTargetRegistry.Initialize();
            Services.Register(soldierTargetRegistry);
            _managers.Add(soldierTargetRegistry);

            var playerControlModeService = new PlayerControlModeService(Events);
            playerControlModeService.Initialize();
            Services.Register(playerControlModeService);
            _managers.Add(playerControlModeService);

            var cameraShakeService = new CameraShakeService(Events);
            cameraShakeService.Initialize();
            Services.Register(cameraShakeService);
            _managers.Add(cameraShakeService);

            _offlineProgressService = new OfflineProgressService(
                Events,
                saveService,
                stageCatalog,
                stageDifficultyConfig,
                playerStats,
                soldierStats,
                soldierCount,
                maxOfflineHours * 3600f,
                offlineRewardMultiplier);

            _lootDropper = new LootDropper(Events, stageCatalog);
            _damageNumberSpawner = new DamageNumberSpawner(Events, poolManager, damageNumberPrefab);
        }

        private void Start()
        {
            // 다른 오브젝트들의 OnEnable(이벤트 구독 포함)이 모두 끝난 뒤(Start 시점)에 호출해야
            // StatEnhancedEvent/OfflineProgressCalculatedEvent를 구독하는 쪽이 이벤트를 놓치지 않는다.
            //
            // CalculateAndApply()를 반드시 가장 먼저 호출해야 한다 — 아래 RestoreLevel 호출들이
            // 발행하는 StatEnhancedEvent/RankChangedEvent를 SaveService가 구독해 즉시 Save()를
            // 호출하는데, Save()는 LastActiveUnixTime을 항상 "지금"으로 덮어쓴다. 오프라인 계산이
            // 그 뒤에 실행되면 이미 덮어써진 시각을 읽어 경과 시간이 0이 되어버린다(실제로 발생했던 버그).
            _offlineProgressService?.CalculateAndApply();

            _enhancementService?.RestoreLevel(EnhancementStatType.AttackPower, _initialSave.AttackPowerLevel);
            _enhancementService?.RestoreLevel(EnhancementStatType.MaxHealth, _initialSave.MaxHealthLevel);
            _enhancementService?.RestoreLevel(EnhancementStatType.AttackSpeed, _initialSave.AttackSpeedLevel);
            _enhancementService?.RestoreLevel(EnhancementStatType.MoveSpeed, _initialSave.MoveSpeedLevel);
            _enhancementService?.RestoreLevel(EnhancementStatType.CriticalChance, _initialSave.CriticalChanceLevel);
            _enhancementService?.RestoreLevel(EnhancementStatType.CriticalDamage, _initialSave.CriticalDamageLevel);

            // RestoreInventory()는 세이브 시딩일 뿐 이벤트를 발행하지 않으므로, 복원된 장착 상태를
            // EquipmentStatReceiver가 놓치지 않도록 여기서 한 번 직접 재계산/발행한다.
            _equipmentStatService?.RecomputeAndPublish();

            _rankService?.RestoreLevel(_initialSave.RankIndex);
            _rankService?.SeedHighestCleared(_initialSave.HighestClearedChapter, _initialSave.HighestClearedStageNumber);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && Services != null && Services.TryGet(out SaveService saveService))
            {
                saveService.Save();
            }
        }

        private void OnApplicationQuit()
        {
            if (Services != null && Services.TryGet(out SaveService saveService))
            {
                saveService.Save();
            }
        }

        private void OnDestroy()
        {
            _lootDropper?.Dispose();
            _lootDropper = null;

            _damageNumberSpawner?.Dispose();
            _damageNumberSpawner = null;

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
