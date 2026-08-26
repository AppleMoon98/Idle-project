using System;
using Behavior;
using Core;
using Dungeon.Events;
using Enhancement;
using Enhancement.Events;
using Equipment;
using Equipment.Events;
using Gacha;
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
using Stage.Events;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// 오프라인 보상 계산에 필요한 최소 게임 상태(재화, 현재/최고 스테이지, 마지막 접속 시각)와
    /// 보유 장비/장착 상태, 보유 병사 로스터(행동 프로필 배정 + 배치 슬롯 배정 포함)를 PlayerPrefs에
    /// 저장/로드한다. 각 스냅샷 조회·복원을 위해 InventoryService/EquippedGearService/
    /// EquipmentCatalogSO, SoldierRosterService/SoldierCatalogSO/SoldierDeploymentService/
    /// BehaviorProfileCatalogSO를 직접 참조한다(EnhancementService가 CurrencyService를 참조하는
    /// 것과 같은 성격의 합성 의존성 — 순수 이벤트 구독만으로는 가변 길이 컬렉션 전체를 스냅샷할 수
    /// 없다).
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

        [Serializable]
        private class SquadTacticSaveBlob
        {
            public SquadTacticService.SquadTacticSnapshotEntry[] Entries;
        }

        [Serializable]
        private class GachaPullCountSaveBlob
        {
            public int[] Counts;
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
        private const string SquadTacticsJsonKey = "Save.SquadTacticsJson";
        private const string SoldierGachaGoldPullCountsJsonKey = "Save.SoldierGachaGoldPullCountsJson";
        private const string SkillGachaGoldPullCountsJsonKey = "Save.SkillGachaGoldPullCountsJson";
        private const string BossTokenCountKey = "Save.BossTokenCount";

        private readonly EventBus _events;
        private readonly InventoryService _inventory;
        private readonly EquippedGearService _equippedGear;
        private readonly EquipmentCatalogSO _equipmentCatalog;
        private readonly SoldierRosterService _soldierRoster;
        private readonly SoldierCatalogSO _soldierCatalog;
        private readonly SoldierDeploymentService _soldierDeployment;
        private readonly BehaviorProfileCatalogSO _behaviorProfileCatalog;
        private readonly SkillService _skill;
        private readonly SkillCatalogSO _skillCatalog;
        private readonly SkillLoadoutService _skillLoadout;
        private readonly SquadTacticService _squadTactic;

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
        private string _squadTacticsJson = "";
        private string _soldierGachaGoldPullCountsJson = "";
        private string _skillGachaGoldPullCountsJson = "";
        private int _bossTokenCount;
        private bool _isDirty;

        // 아래 4개 플래그는 각자의 스냅샷 JSON 재직렬화(Rebuild*Snapshot, 전체 컬렉션을 훑는 비용)를
        // Tick()으로 미루기 위한 더티 플래그다(GitHub 이슈 #21) - 300연 뽑기처럼 같은 프레임 안에서
        // InventoryChangedEvent/SoldierRosterChangedEvent/SkillCountChangedEvent가 수백 번 몰아치는
        // 상황에서, 이벤트가 올 때마다 즉시 전체를 다시 직렬화하면 총 비용이 O(횟수 × 컬렉션 크기)로
        // 커진다(실측: 병사 100/300/600개 순차 추가 시 4ms/20ms/82ms로 초선형 증가). MarkDirty()/
        // Tick()의 기존 PlayerPrefs.Save() 디바운스(section 클래스 doc 참고)와 정확히 같은 방향의
        // 수정 - 그쪽은 디스크 flush를, 이쪽은 재직렬화 자체를 프레임당 최대 한 번으로 묶는다.
        private bool _isInventorySnapshotDirty;
        private bool _isSoldierRosterSnapshotDirty;
        private bool _isSkillLevelsSnapshotDirty;
        private bool _isSkillCountsSnapshotDirty;

        public SaveService(
            EventBus events,
            InventoryService inventory,
            EquippedGearService equippedGear,
            EquipmentCatalogSO equipmentCatalog,
            SoldierRosterService soldierRoster,
            SoldierCatalogSO soldierCatalog,
            SoldierDeploymentService soldierDeployment,
            BehaviorProfileCatalogSO behaviorProfileCatalog,
            SkillService skill,
            SkillCatalogSO skillCatalog,
            SkillLoadoutService skillLoadout,
            SquadTacticService squadTactic)
        {
            _events = events;
            _inventory = inventory;
            _equippedGear = equippedGear;
            _equipmentCatalog = equipmentCatalog;
            _soldierRoster = soldierRoster;
            _soldierCatalog = soldierCatalog;
            _soldierDeployment = soldierDeployment;
            _behaviorProfileCatalog = behaviorProfileCatalog;
            _skill = skill;
            _skillCatalog = skillCatalog;
            _skillLoadout = skillLoadout;
            _squadTactic = squadTactic;
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
            _squadTacticsJson = save.SquadTacticsJson;
            _soldierGachaGoldPullCountsJson = save.SoldierGachaGoldPullCountsJson;
            _skillGachaGoldPullCountsJson = save.SkillGachaGoldPullCountsJson;
            _bossTokenCount = save.BossTokenCount;

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
            _events.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            _events.Subscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            _events.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            _events.Subscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            _events.Subscribe<SkillSlotEnabledChangedEvent>(OnSkillSlotEnabledChanged);
            _events.Subscribe<SkillScrollChangedEvent>(OnSkillScrollChanged);
            _events.Subscribe<EquipmentGachaTicketChangedEvent>(OnEquipmentGachaTicketChanged);
            _events.Subscribe<SquadTacticChangedEvent>(OnSquadTacticChanged);
            _events.Subscribe<SoldierPulledEvent>(OnSoldierPulled);
            _events.Subscribe<SkillPulledEvent>(OnSkillPulled);
            _events.Subscribe<BossTokenChangedEvent>(OnBossTokenChanged);

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
            _events.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            _events.Unsubscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            _events.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            _events.Unsubscribe<SkillLoadoutChangedEvent>(OnSkillLoadoutChanged);
            _events.Unsubscribe<SkillSlotEnabledChangedEvent>(OnSkillSlotEnabledChanged);
            _events.Unsubscribe<SkillScrollChangedEvent>(OnSkillScrollChanged);
            _events.Unsubscribe<EquipmentGachaTicketChangedEvent>(OnEquipmentGachaTicketChanged);
            _events.Unsubscribe<SquadTacticChangedEvent>(OnSquadTacticChanged);
            _events.Unsubscribe<SoldierPulledEvent>(OnSoldierPulled);
            _events.Unsubscribe<SkillPulledEvent>(OnSkillPulled);
            _events.Unsubscribe<BossTokenChangedEvent>(OnBossTokenChanged);
        }

        /// <summary>
        /// 저장된 데이터를 읽는다. 저장 기록이 없으면 LastActiveUnixTime이 0인 기본값을 반환한다.
        /// </summary>
        public SaveData Load()
        {
            BigNumber gold = LoadGold();
            int enhancementStones = ClampNonNegative(PlayerPrefs.GetInt(EnhancementStonesKey, 0));
            int chapter = ClampAtLeastOne(PlayerPrefs.GetInt(ChapterKey, 1));
            int stageNumber = ClampAtLeastOne(PlayerPrefs.GetInt(StageNumberKey, 1));
            int highestClearedChapter = ClampNonNegative(PlayerPrefs.GetInt(HighestClearedChapterKey, 0));
            int highestClearedStageNumber = ClampNonNegative(PlayerPrefs.GetInt(HighestClearedStageNumberKey, 0));
            long lastActiveUnixTime = ParseLastActiveUnixTimeOrZero(PlayerPrefs.GetString(LastActiveUnixTimeKey, "0"));
            int attackPowerLevel = ClampNonNegative(PlayerPrefs.GetInt(AttackPowerLevelKey, 0));
            int maxHealthLevel = ClampNonNegative(PlayerPrefs.GetInt(MaxHealthLevelKey, 0));
            int attackSpeedLevel = ClampNonNegative(PlayerPrefs.GetInt(AttackSpeedLevelKey, 0));
            int moveSpeedLevel = ClampNonNegative(PlayerPrefs.GetInt(MoveSpeedLevelKey, 0));
            int criticalChanceLevel = ClampNonNegative(PlayerPrefs.GetInt(CriticalChanceLevelKey, 0));
            int criticalDamageLevel = ClampNonNegative(PlayerPrefs.GetInt(CriticalDamageLevelKey, 0));
            string inventoryJson = PlayerPrefs.GetString(InventoryJsonKey, "");
            int rankIndex = ClampNonNegative(PlayerPrefs.GetInt(RankIndexKey, 0));
            int soldierTicketCount = ClampNonNegative(PlayerPrefs.GetInt(SoldierTicketCountKey, 0));
            string soldierRosterJson = PlayerPrefs.GetString(SoldierRosterJsonKey, "");
            string skillLevelsJson = PlayerPrefs.GetString(SkillLevelsJsonKey, "");
            int soldierAttackPowerLevel = ClampNonNegative(PlayerPrefs.GetInt(SoldierAttackPowerLevelKey, 0));
            int soldierMaxHealthLevel = ClampNonNegative(PlayerPrefs.GetInt(SoldierMaxHealthLevelKey, 0));
            int soldierAttackSpeedLevel = ClampNonNegative(PlayerPrefs.GetInt(SoldierAttackSpeedLevelKey, 0));
            int soldierMoveSpeedLevel = ClampNonNegative(PlayerPrefs.GetInt(SoldierMoveSpeedLevelKey, 0));
            int soldierCriticalChanceLevel = ClampNonNegative(PlayerPrefs.GetInt(SoldierCriticalChanceLevelKey, 0));
            int soldierCriticalDamageLevel = ClampNonNegative(PlayerPrefs.GetInt(SoldierCriticalDamageLevelKey, 0));
            string skillLoadoutJson = PlayerPrefs.GetString(SkillLoadoutJsonKey, "");
            string skillEnabledJson = PlayerPrefs.GetString(SkillEnabledJsonKey, "");
            int skillScrollCount = ClampNonNegative(PlayerPrefs.GetInt(SkillScrollCountKey, 0));
            string skillCountsJson = PlayerPrefs.GetString(SkillCountsJsonKey, "");
            int equipmentGachaTicketCount = ClampNonNegative(PlayerPrefs.GetInt(EquipmentGachaTicketCountKey, 0));
            string squadTacticsJson = PlayerPrefs.GetString(SquadTacticsJsonKey, "");
            string soldierGachaGoldPullCountsJson = PlayerPrefs.GetString(SoldierGachaGoldPullCountsJsonKey, "");
            string skillGachaGoldPullCountsJson = PlayerPrefs.GetString(SkillGachaGoldPullCountsJsonKey, "");
            int bossTokenCount = ClampNonNegative(PlayerPrefs.GetInt(BossTokenCountKey, 0));

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
                equipmentGachaTicketCount,
                squadTacticsJson,
                soldierGachaGoldPullCountsJson,
                skillGachaGoldPullCountsJson,
                bossTokenCount);
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

            return ClampNonNegative(PlayerPrefs.GetInt(GoldKey, 0));
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
            PlayerPrefs.SetString(SquadTacticsJsonKey, _squadTacticsJson);
            PlayerPrefs.SetString(SoldierGachaGoldPullCountsJsonKey, _soldierGachaGoldPullCountsJson);
            PlayerPrefs.SetString(SkillGachaGoldPullCountsJsonKey, _skillGachaGoldPullCountsJson);
            PlayerPrefs.SetInt(BossTokenCountKey, _bossTokenCount);
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
            if (_isInventorySnapshotDirty)
            {
                RebuildInventorySnapshot();
                _isInventorySnapshotDirty = false;
            }

            if (_isSoldierRosterSnapshotDirty)
            {
                RebuildSoldierRosterSnapshot();
                _isSoldierRosterSnapshotDirty = false;
            }

            if (_isSkillLevelsSnapshotDirty)
            {
                RebuildSkillSnapshot();
                _isSkillLevelsSnapshotDirty = false;
            }

            if (_isSkillCountsSnapshotDirty)
            {
                RebuildSkillCountSnapshot();
                _isSkillCountsSnapshotDirty = false;
            }

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

            // GitHub 이슈 #26 - 두 RestoreSnapshot이 이제 폐기 건수를 구조화된 결과로 돌려주므로,
            // 뭔가 버려졌을 때만(정상 세이브는 거의 항상 0건) 콘솔에 경고를 남긴다 - 이슈 #7이 이미
            // 쓰는 "[SaveService] ..." 로그 관례를 그대로 재사용한다. 배치 복원은 로스터 복원 직후
            // 반드시 이어서 호출해야 한다(SoldierDeploymentService.RestoreSnapshot이 방금 복원된
            // 로스터를 조회해 유령 InstanceId를 걸러낸다).
            SoldierRosterService.RestoreResult rosterResult = _soldierRoster.RestoreSnapshot(blob.Roster, _soldierCatalog, _behaviorProfileCatalog, blob.NextInstanceId);

            if (rosterResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 병사 로스터 복원 중 {rosterResult.TotalDiscarded}건을 버림(카탈로그 없음={rosterResult.DiscardedMissingCatalogEntry}, 음수 ID={rosterResult.DiscardedNegativeInstanceId}, 중복 ID={rosterResult.DiscardedDuplicateInstanceId}) - 복원={rosterResult.RestoredCount}건.");
            }

            SoldierDeploymentService.RestoreResult deploymentResult = _soldierDeployment.RestoreSnapshot(blob.Deployment);

            if (deploymentResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 병사 배치 슬롯 복원 중 {deploymentResult.TotalDiscarded}건을 버림(범위 밖 슬롯={deploymentResult.DiscardedOutOfRangeSlot}, 로스터에 없음={deploymentResult.DiscardedMissingRosterEntry}, 중복 배치={deploymentResult.DiscardedDuplicateInstanceId}) - 복원={deploymentResult.RestoredCount}건.");
            }
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
        /// save.SquadTacticsJson으로 부대별 배정된 전술을 복원한다. GameBootstrapper.Awake()에서
        /// SquadTacticService 생성 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreSquadTactics(SaveData save)
        {
            SquadTacticSaveBlob blob = ParseBlobOrNull<SquadTacticSaveBlob>(save.SquadTacticsJson);

            if (blob == null)
            {
                return;
            }

            // GitHub 이슈 #26 - 로스터/배치와 같은 이유의 진단 로그.
            SquadTacticService.RestoreResult tacticResult = _squadTactic.RestoreSnapshot(blob.Entries);

            if (tacticResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 부대 전술 복원 중 {tacticResult.DiscardedInvalidEntry}건을 버림(범위 밖 SquadIndex 또는 정의되지 않은 Tactic 값) - 복원={tacticResult.RestoredCount}건.");
            }
        }

        /// <summary>
        /// save.SoldierGachaGoldPullCountsJson/SkillGachaGoldPullCountsJson으로 골드 뽑기 누적
        /// 횟수를 복원한다. GachaService/SkillGachaService는 SoldierTicketService/SkillScrollService
        /// 등 자신의 생성자 인자가 save.Load() 결과(SoldierTicketCount 등)에 의존해 SaveService보다
        /// 늦게 생성되므로(GameBootstrapper.Awake() 참고) 이 클래스의 생성자 인자로 받을 수 없다 -
        /// SkillService가 TryLevelUp 시점에 CurrencyService를 GameBootstrapper.Services로 늦게
        /// 조회하는 것과 같은 순환 의존성 우회. GameBootstrapper.Awake()에서 두 서비스 생성 직후
        /// Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreGachaPullCounts(SaveData save)
        {
            GachaPullCountSaveBlob soldierBlob = ParseBlobOrNull<GachaPullCountSaveBlob>(save.SoldierGachaGoldPullCountsJson);

            if (soldierBlob != null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GachaService gacha))
            {
                gacha.RestoreGoldPullCountsSnapshot(soldierBlob.Counts);
            }

            GachaPullCountSaveBlob skillBlob = ParseBlobOrNull<GachaPullCountSaveBlob>(save.SkillGachaGoldPullCountsJson);

            if (skillBlob != null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillGachaService skillGacha))
            {
                skillGacha.RestoreGoldPullCountsSnapshot(skillBlob.Counts);
            }
        }

        /// <summary>
        /// "비어있으면 복원할 게 없고, 아니면 JSON을 역직렬화한다"는 6개 Restore* 메서드가 공유하는
        /// 파싱 절차. JsonUtility.FromJson은 빈 문자열을 넘기면 예외 대신 그냥 null이 아닌 기본
        /// 인스턴스를 반환할 수 있어, 빈 문자열 체크를 JsonUtility 호출보다 먼저 해야 한다.
        /// PlayerPrefs 값이 외부 요인(수동 편집, 저장 도중 비정상 종료 등)으로 손상돼 JSON 형식이
        /// 깨진 경우 JsonUtility.FromJson이 예외(ArgumentException 등)를 던지는데, 이걸 그대로
        /// 두면 GameBootstrapper.Awake() 체인 안에서 호출되는 6개 Restore* 중 하나만 깨져도 부트
        /// 스트랩 전체가 죽는다(실제 GitHub 이슈로 제보됨) — try/catch로 감싸 "복원할 게 없음"과
        /// 동일하게 null을 반환한다. 각 Restore*가 독립된 필드(InventoryJson/SkillLevelsJson 등)를
        /// 각자 파싱하는 구조라, 이 공유 헬퍼 하나만 방어적으로 만들면 한 블롭의 손상이 다른
        /// 블롭 복원에 전혀 영향을 주지 않는다(추가 격리 로직 불필요).
        /// </summary>
        private static T ParseBlobOrNull<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] {typeof(T).Name} 저장 데이터가 손상되어 기본값으로 복원합니다: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// LastActiveUnixTime 저장값을 파싱한다. 형식이 깨졌거나(FormatException/OverflowException)
        /// 음수면 0(= 저장 기록 없음과 동일하게 취급, 오프라인 보상 없이 시작)으로 폴백한다 -
        /// long.Parse를 직접 쓰면 형식이 깨진 값 하나가 Load() 전체를 예외로 중단시켜 멀쩡한
        /// 다른 필드까지 못 읽게 되는 문제가 있었다(실제 GitHub 이슈로 제보됨).
        /// </summary>
        private static long ParseLastActiveUnixTimeOrZero(string raw)
        {
            if (!long.TryParse(raw, out long value) || value < 0)
            {
                return 0;
            }

            return value;
        }

        /// <summary>
        /// 0 미만이 될 수 없는 정수 저장값(재화 레거시 폴백/강화 레벨/티켓·토큰 카운트 등)을
        /// 안전한 기본값(0)으로 클램프한다. PlayerPrefs.GetInt 자체는 손상된 문자열에 대해
        /// 예외를 던지지 않지만, 레지스트리/plist를 직접 편집해 음수를 넣는 것은 여전히 가능하고
        /// 그 값이 그대로 게임 상태에 들어가면 하위 시스템에서 예상치 못한 동작으로 이어질 수 있다
        /// (GitHub 이슈 #7의 "음수·오버플로에 안전한 기본값" 조건).
        /// </summary>
        private static int ClampNonNegative(int value) => value < 0 ? 0 : value;

        /// <summary>
        /// 1 미만이 될 수 없는 정수 저장값(Chapter/StageNumber — 항상 1부터 시작하는 진행 좌표)을
        /// 안전한 최소값(1)으로 클램프한다. ClampNonNegative와 별개로 두는 이유: 이 두 필드는
        /// 0이나 음수가 "기록 없음"이 아니라 카탈로그 인덱스 계산이 깨지는 잘못된 상태다.
        /// </summary>
        private static int ClampAtLeastOne(int value) => value < 1 ? 1 : value;

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

        private void OnBossTokenChanged(BossTokenChangedEvent evt)
        {
            _bossTokenCount = evt.CurrentTokens;
            MarkDirty();
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            _isInventorySnapshotDirty = true;
            MarkDirty();
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            _isInventorySnapshotDirty = true;
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

        private void OnSoldierPulled(SoldierPulledEvent evt)
        {
            RebuildSoldierGachaPullCountsSnapshot();
            MarkDirty();
        }

        private void OnSkillPulled(SkillPulledEvent evt)
        {
            RebuildSkillGachaPullCountsSnapshot();
            MarkDirty();
        }

        private void OnSquadTacticChanged(SquadTacticChangedEvent evt)
        {
            RebuildSquadTacticSnapshot();
            MarkDirty();
        }

        private void OnSoldierRosterChanged(SoldierRosterChangedEvent evt)
        {
            _isSoldierRosterSnapshotDirty = true;
            MarkDirty();
        }

        private void OnSoldierDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            _isSoldierRosterSnapshotDirty = true;
            MarkDirty();
        }

        private void OnSoldierBehaviorProfileChanged(SoldierBehaviorProfileChangedEvent evt)
        {
            _isSoldierRosterSnapshotDirty = true;
            MarkDirty();
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            _isSkillLevelsSnapshotDirty = true;
            MarkDirty();
        }

        private void OnSkillCountChanged(SkillCountChangedEvent evt)
        {
            _isSkillCountsSnapshotDirty = true;
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
        /// SquadTacticService의 현재 부대별 전술 배정 전체를 JSON으로 다시 직렬화한다.
        /// RebuildInventorySnapshot과 같은 이유.
        /// </summary>
        private void RebuildSquadTacticSnapshot()
        {
            var blob = new SquadTacticSaveBlob
            {
                Entries = _squadTactic.ExportSnapshot()
            };

            _squadTacticsJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// GachaService의 현재 골드 뽑기 누적 횟수 전체를 JSON으로 다시 직렬화한다.
        /// RestoreGachaPullCounts와 같은 이유로 GameBootstrapper.Services를 통해 늦게 조회한다.
        /// </summary>
        private void RebuildSoldierGachaPullCountsSnapshot()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out GachaService gacha))
            {
                return;
            }

            var blob = new GachaPullCountSaveBlob
            {
                Counts = gacha.ExportGoldPullCountsSnapshot()
            };

            _soldierGachaGoldPullCountsJson = JsonUtility.ToJson(blob);
        }

        /// <summary>
        /// SkillGachaService 쪽의 RebuildSoldierGachaPullCountsSnapshot과 동일.
        /// </summary>
        private void RebuildSkillGachaPullCountsSnapshot()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillGachaService skillGacha))
            {
                return;
            }

            var blob = new GachaPullCountSaveBlob
            {
                Counts = skillGacha.ExportGoldPullCountsSnapshot()
            };

            _skillGachaGoldPullCountsJson = JsonUtility.ToJson(blob);
        }
    }
}
