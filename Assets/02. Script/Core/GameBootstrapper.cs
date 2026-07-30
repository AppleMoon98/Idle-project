using Character;
using Combat;
using Enhancement;
using Equipment;
using Inventory;
using Loot;
using Managers;
using Offline;
using Save;
using Soldier;
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

        private LootDropper _lootDropper;
        private OfflineProgressService _offlineProgressService;

        private void Awake()
        {
            Services = new ServiceLocator();
            Events = new EventBus();

            Services.Register(Events);
            Services.Register(GetComponent<GameTicker>());

            var poolManager = new PoolManager();
            poolManager.Initialize();
            Services.Register(poolManager);

            var saveService = new SaveService(Events);
            saveService.Initialize();
            Services.Register(saveService);

            SaveData save = saveService.Load();

            var currencyService = new CurrencyService(Events, save.Gold);
            currencyService.Initialize();
            Services.Register(currencyService);

            var enhancementStoneService = new EnhancementStoneService(Events, save.EnhancementStones);
            enhancementStoneService.Initialize();
            Services.Register(enhancementStoneService);

            var inventoryService = new InventoryService(Events);
            inventoryService.Initialize();
            Services.Register(inventoryService);

            var enhancementService = new EnhancementService(Events, currencyService, enhancementConfigs);
            enhancementService.Initialize();
            Services.Register(enhancementService);

            var equipmentFusionService = new EquipmentFusionService(Events, inventoryService, equipmentGradeCatalog, equipmentCatalog);
            equipmentFusionService.Initialize();
            Services.Register(equipmentFusionService);

            var equipmentEnhancementService = new EquipmentEnhancementService(inventoryService, enhancementStoneService, equipmentEnhancementConfig);
            equipmentEnhancementService.Initialize();
            Services.Register(equipmentEnhancementService);

            var soldierTargetRegistry = new SoldierTargetRegistry();
            soldierTargetRegistry.Initialize();
            Services.Register(soldierTargetRegistry);

            var playerTargetTracker = new PlayerTargetTracker();
            playerTargetTracker.Initialize();
            Services.Register(playerTargetTracker);

            _offlineProgressService = new OfflineProgressService(
                Events,
                saveService,
                stageCatalog,
                playerStats,
                soldierStats,
                soldierCount,
                maxOfflineHours * 3600f);

            _lootDropper = new LootDropper(Events);
        }

        private void Start()
        {
            // 다른 오브젝트들의 OnEnable(이벤트 구독 포함)이 모두 끝난 뒤(Start 시점)에 계산해야
            // OfflineProgressCalculatedEvent를 구독하는 UI가 결과를 놓치지 않는다.
            _offlineProgressService?.CalculateAndApply();
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

            if (Services != null && Services.TryGet(out SoldierTargetRegistry soldierTargetRegistry))
            {
                soldierTargetRegistry.Shutdown();
            }

            if (Services != null && Services.TryGet(out PlayerTargetTracker playerTargetTracker))
            {
                playerTargetTracker.Shutdown();
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
