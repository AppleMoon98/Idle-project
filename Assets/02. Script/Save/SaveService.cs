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
using Story.Events;
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
    /// 이벤트 핸들러들은 실제 PlayerPrefs 기록 대신 MarkDirty()로 더티 플래그만 세우고,
    /// FlushPendingChanges()가 프레임당 최대 한 번만 진짜 Save()를 수행한다.
    ///
    /// GitHub 이슈 #28 - GameBootstrapper.OnApplicationPause/OnApplicationQuit은 공개 Save()를
    /// 더 이상 직접 호출하지 않는다. Save()는 4개 컬렉션(장비/병사 로스터·배치/스킬 레벨/스킬
    /// 보유 수) 캐시 문자열이 최신인지 전혀 확인하지 않고 그대로 기록할 뿐이라, 컬렉션이 바뀐
    /// 바로 그 프레임에 Tick이 한 번도 안 돈 채 앱이 중단되면 낡은 캐시가 영구 저장되는 버그가
    /// 있었다. pause/quit도 FlushPendingChanges()를 호출해 "더티 스냅샷 재구축 → 기록 → flush"를
    /// 항상 원자적으로 거치도록 통일했다.
    ///
    /// GitHub 이슈 #49 - FlushPendingChanges()는 더티 상태가 아니면 Save() 자체를 건너뛰므로,
    /// 마지막 저장 이후 변경 사항이 전혀 없는 세션에서 pause/quit이 발생하면 LastActiveUnixTime이
    /// 갱신되지 않아 다음 실행의 오프라인 경과 시간 계산에 그 활성 시간이 이중으로 포함됐다.
    /// GameBootstrapper.OnApplicationPause/OnApplicationQuit은 이제 FlushPendingChanges() 대신
    /// FlushForApplicationLifecycle()을 호출한다 - 더티 상태가 아니었더라도 LastActiveUnixTime만은
    /// 반드시 "지금"으로 갱신한다. Tick()의 매 프레임 경로는 그대로 FlushPendingChanges()만 쓴다.
    ///
    /// GitHub 이슈 #52 - 위 "프레임당 최대 한 번" 디바운스는 같은 프레임에 몰린 이벤트만 묶어줄
    /// 뿐, 서로 다른 프레임에 걸쳐 계속 더티가 유지되는 상황(상시 전투로 골드가 프레임마다 바뀌는
    /// 경우 등)에서는 여전히 프레임마다 Save()(동기 디스크 flush)가 반복 실행됐다. MinSaveInterval
    /// Seconds(기본 2초) 최소 저장 간격을 추가해, 연속된 더티 프레임은 이 간격당 최대 한 번의
    /// 실제 저장으로 병합된다 - _isDirty는 간격을 못 채운 동안 그대로 true로 남으므로 데이터
    /// 유실 없이 디스크 flush 타이밍만 미뤄진다. FlushForApplicationLifecycle()(pause/quit)은
    /// 이 간격을 무시하고 항상 즉시 저장한다.
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
        private const string HighWaterUnixTimeKey = "Save.HighWaterUnixTime"; // GitHub 이슈 #71 - LastActiveUnixTime 직접 편집/롤백 방지용 하이워터마크
        private const string LastElapsedRealtimeSecondsKey = "Save.LastElapsedRealtimeSeconds"; // GitHub 이슈 #71 - Android SystemClock.elapsedRealtime 기반 신호
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
        private const string HasSeenIntroStoryKey = "Save.HasSeenIntroStory";

        /// <summary>
        /// GitHub 이슈 #56 - ResetProgress()가 삭제할 "진행 데이터" 키 전체 목록. 위 상수들과
        /// 정확히 1:1로 대응한다 - 새 Save.* 키를 추가할 때 여기 추가하는 걸 잊으면 그 필드만
        /// 초기화에서 누락되므로, RegressionChecks가 ResetProgress() 이후 Load()가 전부 기본값을
        /// 반환하는지 검증해 누락을 잡아낸다.
        /// </summary>
        private static readonly string[] AllProgressKeys =
        {
            GoldKey, GoldBigKey, EnhancementStonesKey, ChapterKey, StageNumberKey,
            HighestClearedChapterKey, HighestClearedStageNumberKey, LastActiveUnixTimeKey,
            HighWaterUnixTimeKey, LastElapsedRealtimeSecondsKey, AttackPowerLevelKey,
            MaxHealthLevelKey, AttackSpeedLevelKey, MoveSpeedLevelKey, CriticalChanceLevelKey,
            CriticalDamageLevelKey, InventoryJsonKey, RankIndexKey, SoldierTicketCountKey,
            SoldierRosterJsonKey, SkillLevelsJsonKey, SoldierAttackPowerLevelKey,
            SoldierMaxHealthLevelKey, SoldierAttackSpeedLevelKey, SoldierMoveSpeedLevelKey,
            SoldierCriticalChanceLevelKey, SoldierCriticalDamageLevelKey, SkillLoadoutJsonKey,
            SkillEnabledJsonKey, SkillScrollCountKey, SkillCountsJsonKey,
            EquipmentGachaTicketCountKey, SquadTacticsJsonKey, SoldierGachaGoldPullCountsJsonKey,
            SkillGachaGoldPullCountsJsonKey, BossTokenCountKey, HasSeenIntroStoryKey,
        };

        private readonly EventBus _events;
        private readonly InventoryService _inventory;
        private readonly EquippedGearService _equippedGear;
        private readonly EquipmentCatalogSO _equipmentCatalog;
        private readonly EquipmentEnhancementConfigSO _equipmentEnhancementConfig;
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
        private bool _hasSeenIntroStory;
        private bool _isDirty;

        /// <summary>
        /// GitHub 이슈 #52 - 연속된 더티 프레임(상시 전투 중 골드가 프레임마다 바뀌는 경우 등)에서
        /// 실제 디스크 flush(PlayerPrefs.Save())가 반복 실행되는 것을 막기 위한 최소 저장 간격
        /// 정책. 값 자체는 순수 인프라/성능 튜닝값(콘텐츠 밸런스가 아님)이라 SO로 노출하지 않고
        /// 이름 있는 상수로 둔다("매직 넘버 금지" 원칙을 유지하는 이 프로젝트의 기존 관례,
        /// Equipment.EquipmentFusionService.RequiredCountPerFuse와 동일한 성격).
        /// </summary>
        private const float MinSaveIntervalSeconds = 2f;

        // 인스턴스 생성 직후의 "첫 플러시"는 절대 지연시키지 않는다(변경이 드문 평소 플레이에서
        // 체감 지연이 생기지 않도록) - 큰 값으로 시작해 첫 더티 틱이 항상 간격 조건을 통과하게 한다.
        private float _timeSinceLastSave = float.MaxValue;

        /// <summary>
        /// GitHub 이슈 #52 - 실제로 PlayerPrefs.Save()(디스크 flush)가 실행된 누적 횟수. 저장
        /// 빈도를 측정할 수 있는 진단 값으로 공개 노출한다(Character.CharacterSeparation.
        /// BufferGrowthCount 등 이 프로젝트가 이미 쓰는 "진단용 공개 카운터" 관례와 동일한 성격).
        /// </summary>
        public int SaveCallCount { get; private set; }

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
            EquipmentEnhancementConfigSO equipmentEnhancementConfig,
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
            _equipmentEnhancementConfig = equipmentEnhancementConfig;
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
            _hasSeenIntroStory = save.HasSeenIntroStory;

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
            _events.Subscribe<IntroStoryCompletedEvent>(OnIntroStoryCompleted);

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
            _events.Unsubscribe<IntroStoryCompletedEvent>(OnIntroStoryCompleted);
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
            // GitHub 이슈 #71 - LastActiveUnixTime을 저장 파일에서 직접 편집(과거로 되돌림)해도
            // 무력화되도록, 별도로 관리하는 하이워터마크보다 작으면 그 하이워터마크로 올린다.
            // 정상적으로(PersistTrustedNow를 통해) 저장된 값은 두 키가 항상 같으므로 이 클램프는
            // 아무 영향이 없다 - 값이 직접 편집으로 서로 어긋났을 때만 개입한다.
            long rawLastActiveUnixTime = ParseNonNegativeLongOrZero(PlayerPrefs.GetString(LastActiveUnixTimeKey, "0"));
            long highWaterUnixTime = ParseNonNegativeLongOrZero(PlayerPrefs.GetString(HighWaterUnixTimeKey, "0"));
            long lastActiveUnixTime = Math.Max(rawLastActiveUnixTime, highWaterUnixTime);
            long lastElapsedRealtimeSeconds = ParseNonNegativeLongOrZero(PlayerPrefs.GetString(LastElapsedRealtimeSecondsKey, "0"));
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
            bool hasSeenIntroStory = PlayerPrefs.GetInt(HasSeenIntroStoryKey, 0) != 0;

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
                bossTokenCount,
                lastElapsedRealtimeSeconds,
                hasSeenIntroStory);
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
        /// 이 메서드를 직접 부르지 않고 MarkDirty()만 호출한다(FlushPendingChanges()가 프레임당
        /// 최대 한 번으로 묶어서 대신 호출).
        ///
        /// GitHub 이슈 #28 - 예전엔 이 메서드가 GameBootstrapper.OnApplicationPause/
        /// OnApplicationQuit이 앱 종료 직전 직접 호출하는 "안전장치"였다. 문제는 이 메서드가
        /// 캐시된 _inventoryJson/_soldierRosterJson/_skillLevelsJson/_skillCountsJson 문자열을
        /// 그대로 기록할 뿐, 그 문자열이 최신 상태인지(4개 스냅샷 더티 플래그) 전혀 확인하지
        /// 않는다는 것 - 컬렉션이 바뀐 바로 그 프레임에 앱이 중단되면(Tick이 아직 한 번도 안
        /// 돈 상태) 낡은 캐시가 영구 저장되고, _isDirty만 false로 꺼져 다음 저장 기회조차
        /// 사라졌다. pause/quit은 이제 이 메서드를 직접 부르지 않고 FlushPendingChanges()를
        /// 통해서만 호출한다 - 그쪽이 4개 스냅샷을 먼저 최신화한 뒤에야 이 메서드로 넘어온다.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetString(GoldBigKey, _gold.ToString());
            PlayerPrefs.SetInt(EnhancementStonesKey, _enhancementStones);
            PlayerPrefs.SetInt(ChapterKey, _chapter);
            PlayerPrefs.SetInt(StageNumberKey, _stageNumber);
            PlayerPrefs.SetInt(HighestClearedChapterKey, _highestClearedChapter);
            PlayerPrefs.SetInt(HighestClearedStageNumberKey, _highestClearedStageNumber);
            PersistTrustedNow();
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
            PlayerPrefs.SetInt(HasSeenIntroStoryKey, _hasSeenIntroStory ? 1 : 0);
            PlayerPrefs.Save();

            _isDirty = false;
            _timeSinceLastSave = 0f;
            SaveCallCount++;
        }

        /// <summary>
        /// GitHub 이슈 #56 - "게임 데이터 초기화"가 PlayerPrefs.DeleteAll()을 그대로 호출해
        /// 사운드/카메라/확인창 선호 설정(BgmVolume, ScreenShakeDisabled, ConfirmationPopup_
        /// DontShow_* 등 - 전부 다른 클래스가 소유한 별도 PlayerPrefs 키)까지 함께 삭제하던 문제.
        /// 이 메서드는 SaveService 자신이 소유한 진행 데이터 키(Save. 접두사가 붙은 것들, 전부
        /// AllProgressKeys에 나열됨)만 명시적으로 삭제한다 - 그 외 키는 이 클래스가 존재조차
        /// 모르므로 자동으로 보존된다. UI.ResetDataConfirmPopupUI가 유일한 호출부다.
        /// </summary>
        public void ResetProgress()
        {
            foreach (string key in AllProgressKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 게임플레이 이벤트 핸들러가 실제 저장을 요청하는 창구. 즉시 Save()하지 않고 더티
        /// 플래그만 세운다 - 같은 프레임에 여러 이벤트가 몰아쳐도(예: 골드 뽑기 300연의
        /// InventoryChangedEvent) 실제 PlayerPrefs 기록/디스크 flush는 FlushPendingChanges()에서
        /// 한 번만 일어난다(평상시엔 Tick()이, 앱 중단/종료 시점엔 GameBootstrapper가 직접 호출).
        /// </summary>
        private void MarkDirty()
        {
            _isDirty = true;
        }

        void ITickable.Tick(float deltaTime)
        {
            _timeSinceLastSave += deltaTime;
            FlushPendingChanges();
        }

        /// <summary>
        /// GitHub 이슈 #28 - "더티 스냅샷 재구축 → PlayerPrefs 기록 → flush"를 하나로 묶은 원자적
        /// 공개 경로. 평상시엔 Tick()이 프레임당 최대 한 번 호출해 성능 디바운스를 유지하고,
        /// GameBootstrapper.OnApplicationPause/OnApplicationQuit도 이제 Save()를 직접 부르지 않고
        /// FlushForApplicationLifecycle()을 통해 이 로직(의 강제 버전)을 호출한다 - 그래야 컬렉션이
        /// 바뀐 바로 그 프레임에 앱이 중단돼도(Tick이 아직 한 번도 안 돈 상태) 낡은 캐시가 아니라
        /// 최신 상태가 저장된다.
        ///
        /// 4개 스냅샷 더티 플래그와 _isDirty는 항상 같은 호출 지점에서 함께 세워지므로(MarkDirty()가
        /// 스냅샷 dirty 대입 직후 항상 함께 호출됨 - RebuildInventorySnapshot 등 각 세터 참고),
        /// 스냅샷 중 하나라도 재구축했다면 _isDirty는 이미 true라 아래 Save() 호출 조건에 걸린다.
        /// PlayerPrefs.SetString/SetInt/Save() 중 하나가 예외를 던져도 _isDirty=false는 Save()의
        /// 마지막 줄이라 절대 도달하지 못하므로(완료 조건 6), 다음 FlushPendingChanges() 호출이
        /// 그대로 재시도한다 - 이미 재구축된 스냅샷 문자열은 그대로 재사용되므로 중복 재구축도
        /// 일어나지 않는다.
        ///
        /// GitHub 이슈 #52 - 이 public 오버로드는 항상 MinSaveIntervalSeconds 간격 정책을 존중한다
        /// (forceSave:false로 아래 private 오버로드에 위임). Tick()의 매 프레임 호출이 이 경로를
        /// 타므로, 상시 전투처럼 매 프레임 더티여도 실제 PlayerPrefs.Save()(디스크 flush)는 최대
        /// MinSaveIntervalSeconds당 한 번으로 병합된다. 인스턴스 생성 직후의 첫 플러시는
        /// _timeSinceLastSave가 float.MaxValue로 시작하므로 절대 지연되지 않는다 - 변경이 드문
        /// 평소 플레이에서는 체감 지연이 전혀 생기지 않는다.
        /// </summary>
        public void FlushPendingChanges()
        {
            FlushPendingChanges(forceSave: false);
        }

        /// <summary>
        /// GitHub 이슈 #52 - forceSave가 false면(평상시 Tick() 경로) MinSaveIntervalSeconds 간격을
        /// 아직 못 채웠을 때 Save() 자체를 건너뛴다 - _isDirty는 그대로 true로 남아 다음 간격
        /// 도달 시점에 자동으로 저장된다(데이터 유실 없음, 디스크 flush 타이밍만 미뤄짐). forceSave가
        /// true면(FlushForApplicationLifecycle 전용) 간격과 무관하게 더티 상태면 항상 즉시 저장한다
        /// - 일시정지/종료 시점에는 절대 지연 없이 저장돼야 하기 때문(완료 조건 2).
        /// </summary>
        private void FlushPendingChanges(bool forceSave)
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

            if (_isDirty && (forceSave || _timeSinceLastSave >= MinSaveIntervalSeconds))
            {
                Save();
            }
        }

        /// <summary>
        /// GitHub 이슈 #49 - 앱 생명주기 종료 지점(pause/quit) 전용 진입점. FlushPendingChanges()가
        /// 이미 더티 상태라 Save()를 호출했다면 그 안에서 LastActiveUnixTime도 함께 "지금"으로
        /// 갱신되지만(Save()의 기존 동작), 마지막 저장 이후 변경 사항이 전혀 없어 더티 플래그가
        /// 계속 false인 채로 앱이 백그라운드로 가거나 종료되면 LastActiveUnixTime이 아예 갱신되지
        /// 않는다 - 그러면 실제로 앱을 켜 두고 있던 활성 시간이 다음 실행의 오프라인 경과 시간
        /// 계산(Offline.OfflineProgressService.CaptureBudget)에 이중으로 포함된다(실제 GitHub
        /// 이슈로 제보됨: 저장 직후 변경 없이 앱을 켜 뒀다가 종료 → 다음 실행에서 그 활성 시간이
        /// 오프라인 경과 시간에 포함됨). 이 메서드는 그 경우에도 타임스탬프만은 반드시 갱신하도록
        /// 보장한다.
        ///
        /// **Tick()의 매 프레임 호출(FlushPendingChanges 직접 호출) 경로에는 절대 이 보정을 넣지
        /// 않는다** - 매 프레임마다 PlayerPrefs.Save()(디스크 flush)를 강제하면 심각한 성능
        /// 저하가 되기 때문(이슈 #21/#28이 확립한 디바운스 원칙과 정면 배치) - 오직 드물게
        /// 발생하는 pause/quit 생명주기 지점에서만 이 보정이 필요하고 안전하다.
        ///
        /// GitHub 이슈 #52 - FlushPendingChanges(forceSave: true)를 호출해 MinSaveIntervalSeconds
        /// 간격 정책을 우회한다 - 마지막 저장 이후 얼마 지나지 않았어도 더티 상태면 즉시 저장한다.
        /// </summary>
        public void FlushForApplicationLifecycle()
        {
            bool wasDirtyBeforeFlush = _isDirty;

            FlushPendingChanges(forceSave: true);

            if (!wasDirtyBeforeFlush)
            {
                TouchLastActiveTime();
            }
        }

        /// <summary>
        /// LastActiveUnixTime만 "지금"으로 갱신하고 즉시 디스크에 flush한다 - 다른 어떤 필드도
        /// 건드리지 않는다(FlushForApplicationLifecycle 문서 참고).
        /// </summary>
        private void TouchLastActiveTime()
        {
            PersistTrustedNow();
            PlayerPrefs.Save();
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

            // GitHub 이슈 #31 - InventoryService.RestoreSnapshot이 이제 폐기 건수를 구조화된 결과로
            // 돌려주므로, 뭔가 버려졌을 때만(정상 세이브는 거의 항상 0건) 콘솔에 경고를 남긴다 -
            // 이슈 #7/#26이 이미 쓰는 "[SaveService] ..." 로그 관례를 그대로 재사용한다.
            // GitHub 이슈 #50 - EquipmentEnhancementConfigSO.MaxLevel을 함께 넘겨, 설정 최대치를
            // 넘는 강화 레벨이 클램프됐을 때도(폐기와 별개로) 진단 로그를 남긴다.
            InventoryService.RestoreResult inventoryResult = _inventory.RestoreSnapshot(blob.Owned, _equipmentCatalog, _equipmentEnhancementConfig.MaxLevel);

            if (inventoryResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 보유 장비 복원 중 {inventoryResult.TotalDiscarded}건을 버림(카탈로그 없음={inventoryResult.DiscardedMissingCatalogEntry}, 음수 수량={inventoryResult.DiscardedNegativeCount}, 음수 강화 레벨={inventoryResult.DiscardedNegativeEnhancementLevel}) - 복원={inventoryResult.RestoredCount}건.");
            }

            if (inventoryResult.HasCorrectedEntries)
            {
                Debug.LogWarning($"[SaveService] 보유 장비 복원 중 강화 레벨이 설정 최대치({_equipmentEnhancementConfig.MaxLevel})로 보정된 항목 {inventoryResult.CorrectedEnhancementLevelClamped}건 - 손상된 저장 데이터이거나 설정 최대 레벨이 낮아졌을 수 있음(GitHub 이슈 #50).");
            }

            EquippedGearService.RestoreResult equippedResult = _equippedGear.RestoreSnapshot(blob.Equipped, _equipmentCatalog, _inventory);

            if (equippedResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 장착 슬롯 복원 중 {equippedResult.TotalDiscarded}건을 버림(카탈로그 없음={equippedResult.DiscardedMissingCatalogEntry}, 인벤토리 미보유={equippedResult.DiscardedNotInInventory}, 정의되지 않은 슬롯={equippedResult.DiscardedUndefinedSlot}, 슬롯 타입 불일치={equippedResult.DiscardedSlotTypeMismatch}, 중복 장비={equippedResult.DiscardedDuplicateEquipment}) - 복원={equippedResult.RestoredCount}건.");
            }
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

            // GitHub 이슈 #32 - SkillLoadoutService.RestoreSnapshot이 이제 폐기 건수를 구조화된
            // 결과로 돌려주므로, 뭔가 버려졌을 때만(정상 세이브는 거의 항상 0건) 콘솔에 경고를
            // 남긴다 - 이슈 #7/#26/#31이 이미 쓰는 "[SaveService] ..." 로그 관례를 그대로 재사용한다.
            SkillLoadoutService.RestoreResult loadoutResult = _skillLoadout.RestoreSnapshot(blob.Slots, _skillCatalog);

            if (loadoutResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 스킬 장착 슬롯 복원 중 {loadoutResult.TotalDiscarded}건을 버림(범위 밖 슬롯={loadoutResult.DiscardedOutOfRangeSlot}, 카탈로그 없음={loadoutResult.DiscardedMissingCatalogEntry}, 미습득={loadoutResult.DiscardedUnlearnedSkill}, 중복 장착={loadoutResult.DiscardedDuplicateDefinition}) - 복원={loadoutResult.RestoredCount}건.");
            }
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

            SkillLoadoutService.DisabledSlotsRestoreResult enabledResult = _skillLoadout.RestoreDisabledSlots(blob.DisabledSlots);

            if (enabledResult.HasDiscardedEntries)
            {
                Debug.LogWarning($"[SaveService] 스킬 자동 발동 상태 복원 중 {enabledResult.DiscardedOutOfRangeSlot}건을 버림(범위 밖 슬롯) - 복원={enabledResult.RestoredCount}건.");
            }
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
        /// GitHub 이슈 #71 - LastActiveUnixTime을 "지금"으로 쓰는 두 지점(Save/TouchLastActiveTime)이
        /// 공유하는 단일 진입점. 벽시계 값을 그대로 믿지 않고, 별도로 저장해둔 하이워터마크(지금까지
        /// 이 메서드로 실제 기록한 적 있는 가장 늦은 시각)보다 작으면 그 하이워터마크 값으로 올려서
        /// 쓴다 - 그래야 LastActiveUnixTime 저장값을 세이브 파일에서 직접 편집해 과거로 되돌려도,
        /// 그 다음 정상적인 저장 시점에 다시 원래 신뢰 수준으로 복구된다(Load()의 동일한 클램프와
        /// 이중으로 방어). 가능하면(Android) 그 순간의 SystemClock.elapsedRealtime()도 함께
        /// 남겨(재부팅 시 자연 감소는 여기서 그냥 최신값으로 덮어써도 무해하다 - 클래스 doc의
        /// Offline.OfflineElapsedTimeCalculator 설명 참고, 하이워터마크가 필요 없는 이유), 벽시계와
        /// 무관하게 흐르는 신호로 오프라인 경과 시간을 이중 검증할 수 있게 한다.
        /// </summary>
        private void PersistTrustedNow()
        {
            long candidateUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long currentHighWater = ParseNonNegativeLongOrZero(PlayerPrefs.GetString(HighWaterUnixTimeKey, "0"));
            long trustedUnixTime = Math.Max(candidateUnixTime, currentHighWater);

            PlayerPrefs.SetString(LastActiveUnixTimeKey, trustedUnixTime.ToString());
            PlayerPrefs.SetString(HighWaterUnixTimeKey, trustedUnixTime.ToString());

            if (DeviceUptime.TryGetElapsedRealtimeSeconds(out long elapsedRealtimeSeconds))
            {
                PlayerPrefs.SetString(LastElapsedRealtimeSecondsKey, elapsedRealtimeSeconds.ToString());
            }
        }

        /// <summary>
        /// long 문자열로 저장된 시각/카운터 값을 파싱한다(LastActiveUnixTime/HighWaterUnixTime/
        /// LastElapsedRealtimeSeconds가 공유). 형식이 깨졌거나(FormatException/OverflowException)
        /// 음수면 0(= 저장 기록 없음과 동일하게 취급)으로 폴백한다 - long.Parse를 직접 쓰면 형식이
        /// 깨진 값 하나가 Load() 전체를 예외로 중단시켜 멀쩡한 다른 필드까지 못 읽게 되는 문제가
        /// 있었다(실제 GitHub 이슈로 제보됨). 원래 LastActiveUnixTime 전용이었으나 GitHub 이슈
        /// #71에서 시각 관련 필드가 2개 더 생기며 이름을 일반화했다.
        /// </summary>
        private static long ParseNonNegativeLongOrZero(string raw)
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

        private void OnIntroStoryCompleted(IntroStoryCompletedEvent evt)
        {
            _hasSeenIntroStory = true;
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
