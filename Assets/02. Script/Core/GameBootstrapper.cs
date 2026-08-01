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
using Soldier;
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
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private CharacterStatsSO playerStats;

        [SerializeField]
        private CharacterStatsSO soldierStats;

        [SerializeField]
        private int soldierCount;

        [SerializeField]
        private float maxOfflineHours = 24f;

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
        private GachaTableSO gachaTable;

        [SerializeField]
        private SoldierEquipmentCatalogSO soldierEquipmentCatalog;

        [SerializeField]
        private BehaviorProfileCatalogSO behaviorProfileCatalog;

        private LootDropper _lootDropper;
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

            var inventoryService = new InventoryService(Events);
            inventoryService.Initialize();
            Services.Register(inventoryService);

            var equippedGearService = new EquippedGearService(Events);
            equippedGearService.Initialize();
            Services.Register(equippedGearService);

            // SoldierDeploymentService가 슬롯 잠금 해제 수를 물어봐야 해서 RankService를 먼저 만든다.
            _rankService = new RankService(Events, stageCatalog, rankCatalog);
            _rankService.Initialize();
            Services.Register(_rankService);

            var soldierRosterService = new SoldierRosterService(Events);
            soldierRosterService.Initialize();
            Services.Register(soldierRosterService);

            var soldierDeploymentService = new SoldierDeploymentService(Events, soldierRosterService, _rankService);
            soldierDeploymentService.Initialize();
            Services.Register(soldierDeploymentService);

            var soldierEquipmentInventoryService = new SoldierEquipmentInventoryService(Events);
            soldierEquipmentInventoryService.Initialize();
            Services.Register(soldierEquipmentInventoryService);

            var soldierEquippedGearService = new SoldierEquippedGearService(Events);
            soldierEquippedGearService.Initialize();
            Services.Register(soldierEquippedGearService);

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
                soldierEquipmentCatalog);
            saveService.Initialize();
            Services.Register(saveService);

            SaveData save = saveService.Load();
            _initialSave = save;
            saveService.RestoreInventory(save);
            saveService.RestoreSoldierRoster(save);
            saveService.RestoreSoldierEquipment(save);

            var currencyService = new CurrencyService(Events, save.Gold);
            currencyService.Initialize();
            Services.Register(currencyService);

            var enhancementStoneService = new EnhancementStoneService(Events, save.EnhancementStones);
            enhancementStoneService.Initialize();
            Services.Register(enhancementStoneService);

            _enhancementService = new EnhancementService(Events, currencyService, enhancementConfigs);
            _enhancementService.Initialize();
            Services.Register(_enhancementService);

            var equipmentFusionService = new EquipmentFusionService(Events, inventoryService, equipmentGradeCatalog, equipmentCatalog);
            equipmentFusionService.Initialize();
            Services.Register(equipmentFusionService);

            var equipmentEnhancementService = new EquipmentEnhancementService(inventoryService, enhancementStoneService, equipmentEnhancementConfig);
            equipmentEnhancementService.Initialize();
            Services.Register(equipmentEnhancementService);

            _equipmentStatService = new EquipmentStatService(
                Events,
                equippedGearService,
                equipmentGradeCatalog,
                equipmentEnhancementConfig,
                equipmentStatConfig);
            _equipmentStatService.Initialize();
            Services.Register(_equipmentStatService);

            var soldierTicketService = new SoldierTicketService(Events, save.SoldierTicketCount);
            soldierTicketService.Initialize();
            Services.Register(soldierTicketService);

            var gachaService = new GachaService(Events, soldierTicketService, soldierRosterService, gachaTable);
            gachaService.Initialize();
            Services.Register(gachaService);

            var soldierTargetRegistry = new SoldierTargetRegistry();
            soldierTargetRegistry.Initialize();
            Services.Register(soldierTargetRegistry);

            _offlineProgressService = new OfflineProgressService(
                Events,
                saveService,
                stageCatalog,
                playerStats,
                soldierStats,
                soldierCount,
                maxOfflineHours * 3600f);

            _lootDropper = new LootDropper(Events, stageCatalog);
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

            // RestoreInventory()는 세이브 시딩일 뿐 이벤트를 발행하지 않으므로, 복원된 장착 상태를
            // EquipmentStatReceiver가 놓치지 않도록 여기서 한 번 직접 재계산/발행한다.
            _equipmentStatService?.RecomputeAndPublish();

            _rankService?.RestoreLevel(_initialSave.RankIndex);
            _rankService?.CatchUpFromHighestStage(_initialSave.HighestClearedChapter, _initialSave.HighestClearedStageNumber);
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

            if (Services != null && Services.TryGet(out PoolManager poolManager))
            {
                poolManager.Shutdown();
            }

            if (Services != null && Services.TryGet(out CurrencyService currencyService))
            {
                currencyService.Shutdown();
            }

            if (Services != null && Services.TryGet(out EnhancementStoneService enhancementStoneService))
            {
                enhancementStoneService.Shutdown();
            }

            if (Services != null && Services.TryGet(out InventoryService inventoryService))
            {
                inventoryService.Shutdown();
            }

            if (Services != null && Services.TryGet(out EquippedGearService equippedGearService))
            {
                equippedGearService.Shutdown();
            }

            if (Services != null && Services.TryGet(out EnhancementService enhancementService))
            {
                enhancementService.Shutdown();
            }

            if (Services != null && Services.TryGet(out EquipmentFusionService equipmentFusionService))
            {
                equipmentFusionService.Shutdown();
            }

            if (Services != null && Services.TryGet(out EquipmentEnhancementService equipmentEnhancementService))
            {
                equipmentEnhancementService.Shutdown();
            }

            if (Services != null && Services.TryGet(out EquipmentStatService equipmentStatService))
            {
                equipmentStatService.Shutdown();
            }

            if (Services != null && Services.TryGet(out RankService rankService))
            {
                rankService.Shutdown();
            }

            if (Services != null && Services.TryGet(out SoldierTicketService soldierTicketService))
            {
                soldierTicketService.Shutdown();
            }

            if (Services != null && Services.TryGet(out GachaService gachaService))
            {
                gachaService.Shutdown();
            }

            if (Services != null && Services.TryGet(out SoldierRosterService soldierRosterService))
            {
                soldierRosterService.Shutdown();
            }

            if (Services != null && Services.TryGet(out SoldierDeploymentService soldierDeploymentService))
            {
                soldierDeploymentService.Shutdown();
            }

            if (Services != null && Services.TryGet(out SoldierEquipmentInventoryService soldierEquipmentInventoryService))
            {
                soldierEquipmentInventoryService.Shutdown();
            }

            if (Services != null && Services.TryGet(out SoldierEquippedGearService soldierEquippedGearService))
            {
                soldierEquippedGearService.Shutdown();
            }

            if (Services != null && Services.TryGet(out SoldierTargetRegistry soldierTargetRegistry))
            {
                soldierTargetRegistry.Shutdown();
            }

            if (Services != null && Services.TryGet(out SaveService saveService))
            {
                saveService.Shutdown();
            }

            Services?.Clear();
            Events?.Clear();

            Services = null;
            Events = null;
        }
    }
}
