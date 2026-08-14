using System;
using Behavior;
using Core;
using Enhancement;
using Enhancement.Events;
using Equipment;
using Equipment.Events;
using Gacha.Events;
using Inventory;
using Inventory.Events;
using Loot.Events;
using Managers;
using Rank.Events;
using Skill;
using Skill.Events;
using Soldier;
using Soldier.Events;
using SoldierEnhancement.Events;
using SoldierEquipment;
using SoldierEquipment.Events;
using Stage.Events;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// 오프라인 보상 계산에 필요한 최소 게임 상태(재화, 현재/최고 스테이지, 마지막 접속 시각)와
    /// 보유 장비/장착 상태, 보유 병사 로스터(행동 프로필 배정 + 배치 슬롯 배정 포함), 병사 전용 장비
    /// 보유/장착 상태를 PlayerPrefs에 저장/로드한다. 각 스냅샷 조회·복원을 위해
    /// InventoryService/EquippedGearService/EquipmentCatalogSO, SoldierRosterService/SoldierCatalogSO/
    /// SoldierDeploymentService/BehaviorProfileCatalogSO, SoldierEquipmentInventoryService/
    /// SoldierEquippedGearService/SoldierEquipmentCatalogSO를 직접 참조한다(EnhancementService가
    /// CurrencyService를 참조하는 것과 같은 성격의 합성 의존성 — 순수 이벤트 구독만으로는 가변 길이
    /// 컬렉션 전체를 스냅샷할 수 없다).
    ///
    /// 추적 대상 이벤트(골드/장비/스킬/병사 등 20여 종)가 올 때마다 매번 즉시 PlayerPrefs.Save()
    /// (동기 디스크 flush, 이 클래스에서 가장 비싼 호출)까지 실행하면, 골드 뽑기 300연처럼 같은
    /// 프레임 안에서 InventoryChangedEvent가 수백 번 몰아치는 상황에서 그만큼 반복 실행돼 눈에
    /// 띄는 멈춤이 생긴다(실사용 중 발견 — 보유 장비 종류가 많을수록 스냅샷 재직렬화 비용까지
    /// 더해져 심해짐). EquipmentSlotPopupUI의 Refresh() 디바운스(section CL)와 같은 방향으로,
    /// 이벤트 핸들러들은 실제 PlayerPrefs 기록 대신 MarkDirty()로 더티 플래그만 세우고, Tick()이
    /// 프레임당 최대 한 번만 진짜 Save()를 수행한다. 다만 공개 Save()는 그대로 즉시 동기 실행을
    /// 유지한다 — GameBootstrapper.OnApplicationPause/OnApplicationQuit이 앱이 꺼지기 직전
    /// 마지막 상태를 반드시 기록하기 위해 직접 호출하는 안전장치라, 여기 손대면 그 보장이 깨진다.
    /// </summary>
    public sealed class SaveService : IManager, IService, ITickable
    {
        [Serializable]
        private class InventorySaveBlob
        {
            public InventoryService.OwnedEquipmentSnapshot[] Owned;
            public EquippedGearService.EquippedSnapshotEntry[] Equipped;
        }

        [Serializable]
        private class SoldierRosterSaveBlob
        {
            public SoldierRosterService.OwnedSoldierSnapshot[] Roster;
            public int NextInstanceId;
            public SoldierDeploymentService.DeploymentSnapshotEntry[] Deployment;
        }

        [Serializable]
        private class SoldierEquipmentSaveBlob
        {
            public SoldierEquipmentInventoryService.OwnedSoldierEquipmentSnapshot[] Owned;
            public SoldierEquippedGearService.EquippedSnapshotEntry[] Equipped;
        }

        [Serializable]
        private class SkillSaveBlob
        {
            public SkillService.SkillLevelSnapshot[] Levels;
        }

        [Serializable]
        private class SkillCountSaveBlob
        {
            public SkillService.SkillCountSnapshot[] Counts;
        }

        [Serializable]
        private class SkillLoadoutSaveBlob
        {
            public SkillLoadoutService.SkillLoadoutSnapshotEntry[] Slots;
        }

        [Serializable]
        private class SkillEnabledSaveBlob
        {
            public int[] DisabledSlots;
        }

        private const string GoldKey = "Save.Gold"; // 레거시(BigNumber 도입 전) int 저장 키 — 하위호환 읽기 전용
        private const string GoldBigKey = "Save.Gold.Big"; // BigNumber 라운드트립 문자열 저장 키
        private const string EnhancementStonesKey = "Save.EnhancementStones";
        private const string ChapterKey = "Save.Chapter";
        private const string StageNumberKey = "Save.StageNumber";
        private const string HighestClearedChapterKey = "Save.HighestClearedChapter";
        private const string HighestClearedStageNumberKey = "Save.HighestClearedStageNumber";
        private const string LastActiveUnixTimeKey = "Save.LastActiveUnixTime";
        private const string AttackPowerLevelKey = "Save.AttackPowerLevel";
        private const string MaxHealthLevelKey = "Save.MaxHealthLevel";
        private const string AttackSpeedLevelKey = "Save.AttackSpeedLevel";
        private const string MoveSpeedLevelKey = "Save.MoveSpeedLevel";
        private const string CriticalChanceLevelKey = "Save.CriticalChanceLevel";
        private const string CriticalDamageLevelKey = "Save.CriticalDamageLevel";
        private const string InventoryJsonKey = "Save.InventoryJson";
        private const string RankIndexKey = "Save.RankIndex";
        private const string SoldierTicketCountKey = "Save.SoldierTicketCount";
        private const string SoldierRosterJsonKey = "Save.SoldierRosterJson";
        private const string SoldierEquipmentJsonKey = "Save.SoldierEquipmentJson";
        private const string SkillLevelsJsonKey = "Save.SkillLevelsJson";
        private const string SoldierAttackPowerLevelKey = "Save.SoldierAttackPowerLevel";
        private const string SoldierMaxHealthLevelKey = "Save.SoldierMaxHealthLevel";
        private const string SoldierAttackSpeedLevelKey = "Save.SoldierAttackSpeedLevel";
        private const string SoldierMoveSpeedLevelKey = "Save.SoldierMoveSpeedLevel";
        private const string SoldierCriticalChanceLevelKey = "Save.SoldierCriticalChanceLevel";
        private const string SoldierCriticalDamageLevelKey = "Save.SoldierCriticalDamageLevel";
        private const string SkillLoadoutJsonKey = "Save.SkillLoadoutJson";
        private const string SkillEnabledJsonKey = "Save.SkillEnabledJson";
        private const string SkillScrollCountKey = "Save.SkillScrollCount";
        private const string SkillCountsJsonKey = "Save.SkillCountsJson";
        private const string EquipmentGachaTicketCountKey = "Save.EquipmentGachaTicketCount";

        private readonly EventBus _events;
        private readonly InventoryService _inventory;
        private readonly EquippedGearService _equippedGear;
        private readonly EquipmentCatalogSO _equipmentCatalog;
        private readonly SoldierRosterService _soldierRoster;
        private readonly SoldierCatalogSO _soldierCatalog;
        private readonly SoldierDeploymentService _soldierDeployment;
        private readonly BehaviorProfileCatalogSO _behaviorProfileCatalog;
        private readonly SoldierEquipmentInventoryService _soldierEquipmentInventory;
        private readonly SoldierEquippedGearService _soldierEquippedGear;
        private readonly SoldierEquipmentCatalogSO _soldierEquipmentCatalog;
        private readonly SkillService _skill;
        private readonly SkillCatalogSO _skillCatalog;
        private readonly SkillLoadoutService _skillLoadout;

        private BigNumber _gold;
        private int _enhancementStones;
        private int _chapter = 1;
        private int _stageNumber = 1;
        private int _highestClearedChapter;
        private int _highestClearedStageNumber;
        private int _attackPowerLevel;
        private int _maxHealthLevel;
        private int _attackSpeedLevel;
        private int _moveSpeedLevel;
        private int _criticalChanceLevel;
        private int _criticalDamageLevel;
        private string _inventoryJson = "";
        private int _rankIndex;
        private int _soldierTicketCount;
        private string _soldierRosterJson = "";
        private string _soldierEquipmentJson = "";
        private string _skillLevelsJson = "";
        private int _soldierAttackPowerLevel;
        private int _soldierMaxHealthLevel;
        private int _soldierAttackSpeedLevel;
        private int _soldierMoveSpeedLevel;
        private int _soldierCriticalChanceLevel;
        private int _soldierCriticalDamageLevel;
        private string _skillLoadoutJson = "";
        private string _skillEnabledJson = "";
        private int _skillScrollCount;
        private string _skillCountsJson = "";
        private int _equipmentGachaTicketCount;
        private bool _isDirty;

        public SaveService(
            EventBus events,
            InventoryService inventory,
            EquippedGearService equippedGear,
            EquipmentCatalogSO equipmentCatalog,
            SoldierRosterService soldierRoster,
            SoldierCatalogSO soldierCatalog,
            SoldierDeploymentService soldierDeployment,
            BehaviorProfileCatalogSO behaviorProfileCatalog,
            SoldierEquipmentInventoryService soldierEquipmentInventory,
            SoldierEquippedGearService soldierEquippedGear,
            SoldierEquipmentCatalogSO soldierEquipmentCatalog,
            SkillService skill,
            SkillCatalogSO skillCatalog,
            SkillLoadoutService skillLoadout)
        {
            _events = events;
            _inventory = inventory;
            _equippedGear = equippedGear;
            _equipmentCatalog = equipmentCatalog;
            _soldierRoster = soldierRoster;
            _soldierCatalog = soldierCatalog;
            _soldierDeployment = soldierDeployment;
            _behaviorProfileCatalog = behaviorProfileCatalog;
            _soldierEquipmentInventory = soldierEquipmentInventory;
            _soldierEquippedGear = soldierEquippedGear;
            _soldierEquipmentCatalog = soldierEquipmentCatalog;
            _skill = skill;
            _skillCatalog = skillCatalog;
            _skillLoadout = skillLoadout;
        }

        public void Initialize()
        {
            // Save()는 그 시점까지 채워진 내부 필드를 통째로 기록하므로, 이벤트가 아직 한 번도
            // 오지 않은 필드가 기본값(0/1)인 채로 먼저 Save()가 호출되면 저장된 값을 덮어써버린다.
            // 구독 전에 저장된 값을 먼저 채워 어떤 이벤트가 먼저 오든 항상 정확한 값을 기록하게 한다.
            SaveData save = Load();
            _gold = save.Gold;
            _enhancementStones = save.EnhancementStones;
            _chapter = save.Chapter;
            _stageNumber = save.StageNumber;
            _highestClearedChapter = save.HighestClearedChapter;
            _highestClearedStageNumber = save.HighestClearedStageNumber;
            _attackPowerLevel = save.AttackPowerLevel;
            _maxHealthLevel = save.MaxHealthLevel;
            _attackSpeedLevel = save.AttackSpeedLevel;
            _moveSpeedLevel = save.MoveSpeedLevel;
            _criticalChanceLevel = save.CriticalChanceLevel;
            _criticalDamageLevel = save.CriticalDamageLevel;
            _inventoryJson = save.InventoryJson;
            _rankIndex = save.RankIndex;
            _soldierTicketCount = save.SoldierTicketCount;
            _soldierRosterJson = save.SoldierRosterJson;
            _soldierEquipmentJson = save.SoldierEquipmentJson;
            _skillLevelsJson = save.SkillLevelsJson;
            _soldierAttackPowerLevel = save.SoldierAttackPowerLevel;
            _soldierMaxHealthLevel = save.SoldierMaxHealthLevel;
            _soldierAttackSpeedLevel = save.SoldierAttackSpeedLevel;
            _soldierMoveSpeedLevel = save.SoldierMoveSpeedLevel;
            _soldierCriticalChanceLevel = save.SoldierCriticalChanceLevel;
            _soldierCriticalDamageLevel = save.SoldierCriticalDamageLevel;
            _skillLoadoutJson = save.SkillLoadoutJson;
            _skillEnabledJson = save.SkillEnabledJson;
            _skillScrollCount = save.SkillScrollCount;
            _skillCountsJson = save.SkillCountsJson;
            _equipmentGachaTicketCount = save.EquipmentGachaTicketCount;

            _events.Subscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Subscribe<EnhancementStoneChangedEvent>(OnEnhancementStoneChanged);
            _events.Subscribe<StageChangedEvent>(OnStageChanged);
            _events.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            _events.Subscribe<StatEnhancedEvent>(OnStatEnhanced);
            _events.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            _events.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            _events.Subscribe<RankChangedEvent>(OnRankChanged);
            _events.Subscribe<SoldierTicketChangedEvent>(OnSoldierTicketChanged);
            _events.Subscribe<SoldierRosterChangedEvent>(OnSoldierRosterChanged);
            _events.Subscribe<SoldierDeploymentChangedEvent>(OnSoldierDeploymentChanged);
            _events.Subscribe<SoldierBehaviorProfileChangedEvent>(OnSoldierBehaviorProfileChanged);
            _events.Subscribe<SoldierEquipmentInventoryChangedEvent>(OnSoldierEquipmentInventoryChanged);
            _events.Subscribe<SoldierEquipmentEquippedEvent>(OnSoldierEquipmentEquipped);
            _events.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            _events.Subscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            _events.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            _events.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            _events.Subscribe<SkillSlotEnabledChangedEvent>(OnSkillSlotEnabledChanged);
            _events.Subscribe<SkillScrollChangedEvent>(OnSkillScrollChanged);
            _events.Subscribe<EquipmentGachaTicketChangedEvent>(OnEquipmentGachaTicketChanged);

            TickerRegistration.Register(this);
        }

        public void Shutdown()
        {
            TickerRegistration.Unregister(this);

            _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Unsubscribe<EnhancementStoneChangedEvent>(OnEnhancementStoneChanged);
            _events.Unsubscribe<StageChangedEvent>(OnStageChanged);
            _events.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            _events.Unsubscribe<StatEnhancedEvent>(OnStatEnhanced);
            _events.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            _events.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            _events.Unsubscribe<RankChangedEvent>(OnRankChanged);
            _events.Unsubscribe<SoldierTicketChangedEvent>(OnSoldierTicketChanged);
            _events.Unsubscribe<SoldierRosterChangedEvent>(OnSoldierRosterChanged);
            _events.Unsubscribe<SoldierDeploymentChangedEvent>(OnSoldierDeploymentChanged);
            _events.Unsubscribe<SoldierBehaviorProfileChangedEvent>(OnSoldierBehaviorProfileChanged);
            _events.Unsubscribe<SoldierEquipmentInventoryChangedEvent>(OnSoldierEquipmentInventoryChanged);
            _events.Unsubscribe<SoldierEquipmentEquippedEvent>(OnSoldierEquipmentEquipped);
            _events.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            _events.Unsubscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            _events.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            _events.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            _events.Unsubscribe<SkillSlotEnabledChangedEvent>(OnSkillSlotEnabledChanged);
            _events.Unsubscribe<SkillScrollChangedEvent>(OnSkillScrollChanged);
            _events.Unsubscribe<EquipmentGachaTicketChangedEvent>(OnEquipmentGachaTicketChanged);
        }

        /// <summary>
        /// 저장된 데이터를 읽는다. 저장 기록이 없으면 LastActiveUnixTime이 0인 기본값을 반환한다.
        /// </summary>
        public SaveData Load()
        {
            BigNumber gold = LoadGold();
            int enhancementStones = PlayerPrefs.GetInt(EnhancementStonesKey, 0);
            int chapter = PlayerPrefs.GetInt(ChapterKey, 1);
            int stageNumber = PlayerPrefs.GetInt(StageNumberKey, 1);
            int highestClearedChapter = PlayerPrefs.GetInt(HighestClearedChapterKey, 0);
            int highestClearedStageNumber = PlayerPrefs.GetInt(HighestClearedStageNumberKey, 0);
            long lastActiveUnixTime = long.Parse(PlayerPrefs.GetString(LastActiveUnixTimeKey, "0"));
            int attackPowerLevel = PlayerPrefs.GetInt(AttackPowerLevelKey, 0);
            int maxHealthLevel = PlayerPrefs.GetInt(MaxHealthLevelKey, 0);
            int attackSpeedLevel = PlayerPrefs.GetInt(AttackSpeedLevelKey, 0);
            int moveSpeedLevel = PlayerPrefs.GetInt(MoveSpeedLevelKey, 0);
            int criticalChanceLevel = PlayerPrefs.GetInt(CriticalChanceLevelKey, 0);
            int criticalDamageLevel = PlayerPrefs.GetInt(CriticalDamageLevelKey, 0);
            string inventoryJson = PlayerPrefs.GetString(InventoryJsonKey, "");
            int rankIndex = PlayerPrefs.GetInt(RankIndexKey, 0);
            int soldierTicketCount = PlayerPrefs.GetInt(SoldierTicketCountKey, 0);
            string soldierRosterJson = PlayerPrefs.GetString(SoldierRosterJsonKey, "");
            string soldierEquipmentJson = PlayerPrefs.GetString(SoldierEquipmentJsonKey, "");
            string skillLevelsJson = PlayerPrefs.GetString(SkillLevelsJsonKey, "");
            int soldierAttackPowerLevel = PlayerPrefs.GetInt(SoldierAttackPowerLevelKey, 0);
            int soldierMaxHealthLevel = PlayerPrefs.GetInt(SoldierMaxHealthLevelKey, 0);
            int soldierAttackSpeedLevel = PlayerPrefs.GetInt(SoldierAttackSpeedLevelKey, 0);
            int soldierMoveSpeedLevel = PlayerPrefs.GetInt(SoldierMoveSpeedLevelKey, 0);
            int soldierCriticalChanceLevel = PlayerPrefs.GetInt(SoldierCriticalChanceLevelKey, 0);
            int soldierCriticalDamageLevel = PlayerPrefs.GetInt(SoldierCriticalDamageLevelKey, 0);
            string skillLoadoutJson = PlayerPrefs.GetString(SkillLoadoutJsonKey, "");
            string skillEnabledJson = PlayerPrefs.GetString(SkillEnabledJsonKey, "");
            int skillScrollCount = PlayerPrefs.GetInt(SkillScrollCountKey, 0);
            string skillCountsJson = PlayerPrefs.GetString(SkillCountsJsonKey, "");
            int equipmentGachaTicketCount = PlayerPrefs.GetInt(EquipmentGachaTicketCountKey, 0);

            return new SaveData(
                gold,
                enhancementStones,
                chapter,
                stageNumber,
                highestClearedChapter,
                highestClearedStageNumber,
                lastActiveUnixTime,
                attackPowerLevel,
                maxHealthLevel,
                attackSpeedLevel,
                moveSpeedLevel,
                criticalChanceLevel,
                criticalDamageLevel,
                inventoryJson,
                rankIndex,
                soldierTicketCount,
                soldierRosterJson,
                soldierEquipmentJson,
                skillLevelsJson,
                soldierAttackPowerLevel,
                soldierMaxHealthLevel,
                soldierAttackSpeedLevel,
                soldierMoveSpeedLevel,
                soldierCriticalChanceLevel,
                soldierCriticalDamageLevel,
                skillLoadoutJson,
                skillEnabledJson,
                skillScrollCount,
                skillCountsJson,
                equipmentGachaTicketCount);
        }

        /// <summary>
        /// 골드를 읽는다. BigNumber 문자열 저장분을 우선 시도하고, 없으면(= BigNumber 도입 전
        /// int로 저장된 구버전 세이브) 레거시 int 키로 폴백한다.
        /// </summary>
        private static BigNumber LoadGold()
        {
            string raw = PlayerPrefs.GetString(GoldBigKey, "");

            if (BigNumber.TryParse(raw, out BigNumber parsed))
            {
                return parsed;
            }

            return PlayerPrefs.GetInt(GoldKey, 0);
        }

        /// <summary>
        /// 지금까지 추적한 값과 현재 시각을 PlayerPrefs에 즉시(동기) 기록한다 — PlayerPrefs.Save()의
        /// 디스크 flush를 포함하므로 이 클래스에서 가장 비싼 호출이다. 게임플레이 이벤트 핸들러는
        /// 이 메서드를 직접 부르지 않고 MarkDirty()만 호출한다(Tick()이 프레임당 최대 한 번으로
        /// 묶어서 대신 호출) — 이 공개 메서드 자체는 GameBootstrapper.OnApplicationPause/
        /// OnApplicationQuit이 앱 종료 직전 마지막 상태를 확실히 기록하려고 직접 호출하는
        /// 안전장치이므로 즉시 실행 동작을 그대로 유지해야 한다.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetString(GoldBigKey, _gold.ToString());
            PlayerPrefs.SetInt(EnhancementStonesKey, _enhancementStones);
            PlayerPrefs.SetInt(ChapterKey, _chapter);
            PlayerPrefs.SetInt(StageNumberKey, _stageNumber);
            PlayerPrefs.SetInt(HighestClearedChapterKey, _highestClearedChapter);
            PlayerPrefs.SetInt(HighestClearedStageNumberKey, _highestClearedStageNumber);
            PlayerPrefs.SetString(LastActiveUnixTimeKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.SetInt(AttackPowerLevelKey, _attackPowerLevel);
            PlayerPrefs.SetInt(MaxHealthLevelKey, _maxHealthLevel);
            PlayerPrefs.SetInt(AttackSpeedLevelKey, _attackSpeedLevel);
            PlayerPrefs.SetInt(MoveSpeedLevelKey, _moveSpeedLevel);
            PlayerPrefs.SetInt(CriticalChanceLevelKey, _criticalChanceLevel);
            PlayerPrefs.SetInt(CriticalDamageLevelKey, _criticalDamageLevel);
            PlayerPrefs.SetString(InventoryJsonKey, _inventoryJson);
            PlayerPrefs.SetInt(RankIndexKey, _rankIndex);
            PlayerPrefs.SetInt(SoldierTicketCountKey, _soldierTicketCount);
            PlayerPrefs.SetString(SoldierRosterJsonKey, _soldierRosterJson);
            PlayerPrefs.SetString(SoldierEquipmentJsonKey, _soldierEquipmentJson);
            PlayerPrefs.SetString(SkillLevelsJsonKey, _skillLevelsJson);
            PlayerPrefs.SetInt(SoldierAttackPowerLevelKey, _soldierAttackPowerLevel);
            PlayerPrefs.SetInt(SoldierMaxHealthLevelKey, _soldierMaxHealthLevel);
            PlayerPrefs.SetInt(SoldierAttackSpeedLevelKey, _soldierAttackSpeedLevel);
            PlayerPrefs.SetInt(SoldierMoveSpeedLevelKey, _soldierMoveSpeedLevel);
            PlayerPrefs.SetInt(SoldierCriticalChanceLevelKey, _soldierCriticalChanceLevel);
            PlayerPrefs.SetInt(SoldierCriticalDamageLevelKey, _soldierCriticalDamageLevel);
            PlayerPrefs.SetString(SkillLoadoutJsonKey, _skillLoadoutJson);
            PlayerPrefs.SetString(SkillEnabledJsonKey, _skillEnabledJson);
            PlayerPrefs.SetInt(SkillScrollCountKey, _skillScrollCount);
            PlayerPrefs.SetString(SkillCountsJsonKey, _skillCountsJson);
            PlayerPrefs.SetInt(EquipmentGachaTicketCountKey, _equipmentGachaTicketCount);
            PlayerPrefs.Save();

            _isDirty = false;
        }

        /// <summary>
        /// 게임플레이 이벤트 핸들러가 실제 저장을 요청하는 창구. 즉시 Save()하지 않고 더티
        /// 플래그만 세운다 - 같은 프레임에 여러 이벤트가 몰아쳐도(예: 골드 뽑기 300연의
        /// InventoryChangedEvent) 실제 PlayerPrefs 기록/디스크 flush는 Tick()에서 한 번만 일어난다.
        /// </summary>
        private void MarkDirty()
        {
            _isDirty = true;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isDirty)
            {
                Save();
            }
        }

        /// <summary>
        /// save.InventoryJson으로 보유 장비/장착 상태를 복원한다. GameBootstrapper.Awake()에서
        /// InventoryService/EquippedGearService 생성 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreInventory(SaveData save)
        {
            InventorySaveBlob blob = ParseBlobOrNull<InventorySaveBlob>(save.InventoryJson);

            if (blob == null)
            {
                return;
            }

            _inventory.RestoreSnapshot(blob.Owned, _equipmentCatalog);
            _equippedGear.RestoreSnapshot(blob.Equipped, _equipmentCatalog, _inventory);
        }

        /// <summary>
        /// save.SoldierRosterJson으로 보유 병사 로스터(행동 프로필 배정 포함)와 배치 슬롯 배정을 복원한다.
        /// GameBootstrapper.Awake()에서 SoldierRosterService/SoldierDeploymentService 생성 직후,
        /// Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSoldierRoster(SaveData save)
        {
            SoldierRosterSaveBlob blob = ParseBlobOrNull<SoldierRosterSaveBlob>(save.SoldierRosterJson);

            if (blob == null)
            {
                return;
            }

            _soldierRoster.RestoreSnapshot(blob.Roster, _soldierCatalog, _behaviorProfileCatalog, blob.NextInstanceId);
            _soldierDeployment.RestoreSnapshot(blob.Deployment);
        }

        /// <summary>
        /// save.SoldierEquipmentJson으로 병사 전용 장비의 보유 재고/장착 상태를 복원한다.
        /// GameBootstrapper.Awake()에서 관련 서비스 생성 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSoldierEquipment(SaveData save)
        {
            SoldierEquipmentSaveBlob blob = ParseBlobOrNull<SoldierEquipmentSaveBlob>(save.SoldierEquipmentJson);

            if (blob == null)
            {
                return;
            }

            _soldierEquipmentInventory.RestoreSnapshot(blob.Owned, _soldierEquipmentCatalog);
            _soldierEquippedGear.RestoreSnapshot(blob.Equipped, _soldierEquipmentCatalog, _soldierEquipmentInventory);
        }

        /// <summary>
        /// save.SkillLevelsJson으로 스킬별 레벨을 복원한다. GameBootstrapper.Awake()에서
        /// SkillService 생성 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSkills(SaveData save)
        {
            SkillSaveBlob blob = ParseBlobOrNull<SkillSaveBlob>(save.SkillLevelsJson);

            if (blob == null)
            {
                return;
            }

            _skill.RestoreSnapshot(blob.Levels, _skillCatalog);
        }

        /// <summary>
        /// save.SkillCountsJson으로 스킬별 보유 개수를 복원한다. GameBootstrapper.Awake()에서
        /// RestoreSkills 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSkillCounts(SaveData save)
        {
            SkillCountSaveBlob blob = ParseBlobOrNull<SkillCountSaveBlob>(save.SkillCountsJson);

            if (blob == null)
            {
                return;
            }

            _skill.RestoreCountSnapshot(blob.Counts, _skillCatalog);
        }

        /// <summary>
        /// save.SkillLoadoutJson으로 슬롯별 장착 스킬을 복원한다. GameBootstrapper.Awake()에서
        /// SkillLoadoutService 생성 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSkillLoadout(SaveData save)
        {
            SkillLoadoutSaveBlob blob = ParseBlobOrNull<SkillLoadoutSaveBlob>(save.SkillLoadoutJson);

            if (blob == null)
            {
                return;
            }

            _skillLoadout.RestoreSnapshot(blob.Slots, _skillCatalog);
        }

        /// <summary>
        /// save.SkillEnabledJson으로 슬롯별 자동 발동 켜짐/꺼짐 상태를 복원한다. GameBootstrapper.Awake()에서
        /// RestoreSkillLoadout 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSkillEnabled(SaveData save)
        {
            SkillEnabledSaveBlob blob = ParseBlobOrNull<SkillEnabledSaveBlob>(save.SkillEnabledJson);

            if (blob == null)
            {
                return;
            }

            _skillLoadout.RestoreDisabledSlots(blob.DisabledSlots);
        }

        /// <summary>
        /// "비어있으면 복원할 게 없고, 아니면 JSON을 역직렬화한다"는 6개 Restore* 메서드가 공유하는
        /// 파싱 절차. JsonUtility.FromJson은 빈 문자열을 넘기면 예외 대신 그냥 null이 아닌 기본
        /// 인스턴스를 반환할 수 있어, 빈 문자열 체크를 JsonUtility 호출보다 먼저 해야 한다.
        /// </summary>
        private static T ParseBlobOrNull<T>(string json) where T : class
        {
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            _gold = evt.CurrentGold;
            MarkDirty();
        }

        private void OnEnhancementStoneChanged(EnhancementStoneChangedEvent evt)
        {
            _enhancementStones = evt.CurrentStones;
            MarkDirty();
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _chapter = evt.Chapter;
            _stageNumber = evt.StageNumber;
            MarkDirty();
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            _highestClearedChapter = evt.Chapter;
            _highestClearedStageNumber = evt.StageNumber;
            MarkDirty();
        }

        private void OnStatEnhanced(StatEnhancedEvent evt)
        {
            switch (evt.StatType)
            {
                case EnhancementStatType.AttackPower:
                    _attackPowerLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.MaxHealth:
                    _maxHealthLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.AttackSpeed:
                    _attackSpeedLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.MoveSpeed:
                    _moveSpeedLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.CriticalChance:
                    _criticalChanceLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.CriticalDamage:
                    _criticalDamageLevel = evt.NewLevel;
                    break;
            }

            MarkDirty();
        }

        private void OnSoldierStatEnhanced(SoldierStatEnhancedEvent evt)
        {
            switch (evt.StatType)
            {
                case EnhancementStatType.AttackPower:
                    _soldierAttackPowerLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.MaxHealth:
                    _soldierMaxHealthLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.AttackSpeed:
                    _soldierAttackSpeedLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.MoveSpeed:
                    _soldierMoveSpeedLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.CriticalChance:
                    _soldierCriticalChanceLevel = evt.NewLevel;
                    break;
                case EnhancementStatType.CriticalDamage:
                    _soldierCriticalDamageLevel = evt.NewLevel;
                    break;
            }

            MarkDirty();
        }

        private void OnSkillLoadoutChanged(SkillLoadoutChangedEvent evt)
        {
            RebuildSkillLoadoutSnapshot();
            MarkDirty();
        }

        private void OnSkillSlotEnabledChanged(SkillSlotEnabledChangedEvent evt)
        {
            RebuildSkillEnabledSnapshot();
            MarkDirty();
        }

        private void OnSkillScrollChanged(SkillScrollChangedEvent evt)
        {
            _skillScrollCount = evt.CurrentScrolls;
            MarkDirty();
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            RebuildInventorySnapshot();
            MarkDirty();
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            RebuildInventorySnapshot();
            MarkDirty();
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            _rankIndex = evt.NewRankIndex;
            MarkDirty();
        }

        private void OnSoldierTicketChanged(SoldierTicketChangedEvent evt)
        {
            _soldierTicketCount = evt.CurrentTickets;
            MarkDirty();
        }

        private void OnEquipmentGachaTicketChanged(EquipmentGachaTicketChangedEvent evt)
        {
            _equipmentGachaTicketCount = evt.CurrentTickets;
            MarkDirty();
        }

        private void OnSoldierRosterChanged(SoldierRosterChangedEvent evt)
        {
            RebuildRosterSnapshotAndSave();
        }

        private void OnSoldierDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            RebuildRosterSnapshotAndSave();
        }

        private void OnSoldierBehaviorProfileChanged(SoldierBehaviorProfileChangedEvent evt)
        {
            RebuildRosterSnapshotAndSave();
        }

        private void RebuildRosterSnapshotAndSave()
        {
            RebuildSoldierRosterSnapshot();
            MarkDirty();
        }

        private void OnSoldierEquipmentInventoryChanged(SoldierEquipmentInventoryChangedEvent evt)
        {
            RebuildSoldierEquipmentSnapshot();
            MarkDirty();
        }

        private void OnSoldierEquipmentEquipped(SoldierEquipmentEquippedEvent evt)
        {
            RebuildSoldierEquipmentSnapshot();
            MarkDirty();
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            RebuildSkillSnapshot();
            MarkDirty();
        }

        private void OnSkillCountChanged(SkillCountChangedEvent evt)
        {
            RebuildSkillCountSnapshot();
            MarkDirty();
        }

        /// <summary>
        /// SkillService의 현재 상태 전체를 JSON으로 다시 직렬화한다. RebuildInventorySnapshot과 같은 이유.
        /// </summary>
        private void RebuildSkillSnapshot()
        {
            var blob = new SkillSaveBlob
            {
                Levels = _skill.ExportSnapshot(_skillCatalog)
            };

            _skillLevelsJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// SkillService의 현재 보유 개수 전체를 JSON으로 다시 직렬화한다. RebuildSkillSnapshot과 같은 이유.
        /// </summary>
        private void RebuildSkillCountSnapshot()
        {
            var blob = new SkillCountSaveBlob
            {
                Counts = _skill.ExportCountSnapshot(_skillCatalog)
            };

            _skillCountsJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// SkillLoadoutService의 현재 장착 상태 전체를 JSON으로 다시 직렬화한다. RebuildInventorySnapshot과 같은 이유.
        /// </summary>
        private void RebuildSkillLoadoutSnapshot()
        {
            var blob = new SkillLoadoutSaveBlob
            {
                Slots = _skillLoadout.ExportSnapshot(_skillCatalog)
            };

            _skillLoadoutJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// SkillLoadoutService의 현재 켜짐/꺼짐 상태 전체를 JSON으로 다시 직렬화한다. RebuildSkillLoadoutSnapshot과 같은 이유.
        /// </summary>
        private void RebuildSkillEnabledSnapshot()
        {
            var blob = new SkillEnabledSaveBlob
            {
                DisabledSlots = _skillLoadout.ExportDisabledSlots()
            };

            _skillEnabledJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// InventoryService/EquippedGearService의 현재 상태 전체를 JSON으로 다시 직렬화한다.
        /// 이벤트는 "무언가 바뀌었다"만 알려주므로, 저장할 땐 항상 전체 스냅샷을 새로 만든다.
        /// </summary>
        private void RebuildInventorySnapshot()
        {
            var blob = new InventorySaveBlob
            {
                Owned = _inventory.ExportSnapshot(_equipmentCatalog),
                Equipped = _equippedGear.ExportSnapshot(_equipmentCatalog)
            };

            _inventoryJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// SoldierRosterService의 현재 상태 전체를 JSON으로 다시 직렬화한다. RebuildInventorySnapshot과 같은 이유.
        /// </summary>
        private void RebuildSoldierRosterSnapshot()
        {
            SoldierRosterService.OwnedSoldierSnapshot[] roster = _soldierRoster.ExportSnapshot(_soldierCatalog, _behaviorProfileCatalog, out int nextInstanceId);

            var blob = new SoldierRosterSaveBlob
            {
                Roster = roster,
                NextInstanceId = nextInstanceId,
                Deployment = _soldierDeployment.ExportSnapshot()
            };

            _soldierRosterJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// SoldierEquipmentInventoryService/SoldierEquippedGearService의 현재 상태 전체를 JSON으로
        /// 다시 직렬화한다. RebuildInventorySnapshot과 같은 이유.
        /// </summary>
        private void RebuildSoldierEquipmentSnapshot()
        {
            var blob = new SoldierEquipmentSaveBlob
            {
                Owned = _soldierEquipmentInventory.ExportSnapshot(_soldierEquipmentCatalog),
                Equipped = _soldierEquippedGear.ExportSnapshot(_soldierEquipmentCatalog)
            };

            _soldierEquipmentJson = JsonUtility.ToJson(blob);
        }
    }
}
