using System;
using Core;
using Enhancement;
using Enhancement.Events;
using Equipment;
using Equipment.Events;
using Inventory;
using Inventory.Events;
using Loot.Events;
using Rank.Events;
using Stage.Events;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// 오프라인 보상 계산에 필요한 최소 게임 상태(재화, 현재/최고 스테이지, 마지막 접속 시각)와
    /// 보유 장비/장착 상태를 PlayerPrefs에 저장/로드한다. 인벤토리 스냅샷 조회/복원을 위해
    /// InventoryService/EquippedGearService/EquipmentCatalogSO를 직접 참조한다(EnhancementService가
    /// CurrencyService를 참조하는 것과 같은 성격의 합성 의존성 — 순수 이벤트 구독만으로는
    /// 가변 길이 컬렉션 전체를 스냅샷할 수 없다).
    /// </summary>
    public sealed class SaveService : IManager, IService
    {
        [Serializable]
        private class InventorySaveBlob
        {
            public InventoryService.OwnedEquipmentSnapshot[] Owned;
            public EquippedGearService.EquippedSnapshotEntry[] Equipped;
        }

        private const string GoldKey = "Save.Gold";
        private const string EnhancementStonesKey = "Save.EnhancementStones";
        private const string ChapterKey = "Save.Chapter";
        private const string StageNumberKey = "Save.StageNumber";
        private const string HighestClearedChapterKey = "Save.HighestClearedChapter";
        private const string HighestClearedStageNumberKey = "Save.HighestClearedStageNumber";
        private const string LastActiveUnixTimeKey = "Save.LastActiveUnixTime";
        private const string AttackPowerLevelKey = "Save.AttackPowerLevel";
        private const string MaxHealthLevelKey = "Save.MaxHealthLevel";
        private const string InventoryJsonKey = "Save.InventoryJson";
        private const string RankIndexKey = "Save.RankIndex";

        private readonly EventBus _events;
        private readonly InventoryService _inventory;
        private readonly EquippedGearService _equippedGear;
        private readonly EquipmentCatalogSO _equipmentCatalog;

        private int _gold;
        private int _enhancementStones;
        private int _chapter = 1;
        private int _stageNumber = 1;
        private int _highestClearedChapter;
        private int _highestClearedStageNumber;
        private int _attackPowerLevel;
        private int _maxHealthLevel;
        private string _inventoryJson = "";
        private int _rankIndex;

        public SaveService(EventBus events, InventoryService inventory, EquippedGearService equippedGear, EquipmentCatalogSO equipmentCatalog)
        {
            _events = events;
            _inventory = inventory;
            _equippedGear = equippedGear;
            _equipmentCatalog = equipmentCatalog;
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
            _inventoryJson = save.InventoryJson;
            _rankIndex = save.RankIndex;

            _events.Subscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Subscribe<EnhancementStoneChangedEvent>(OnEnhancementStoneChanged);
            _events.Subscribe<StageChangedEvent>(OnStageChanged);
            _events.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            _events.Subscribe<StatEnhancedEvent>(OnStatEnhanced);
            _events.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            _events.Subscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            _events.Subscribe<RankChangedEvent>(OnRankChanged);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Unsubscribe<EnhancementStoneChangedEvent>(OnEnhancementStoneChanged);
            _events.Unsubscribe<StageChangedEvent>(OnStageChanged);
            _events.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            _events.Unsubscribe<StatEnhancedEvent>(OnStatEnhanced);
            _events.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            _events.Unsubscribe<EquipmentEquippedEvent>(OnEquipmentEquipped);
            _events.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        /// <summary>
        /// 저장된 데이터를 읽는다. 저장 기록이 없으면 LastActiveUnixTime이 0인 기본값을 반환한다.
        /// </summary>
        public SaveData Load()
        {
            int gold = PlayerPrefs.GetInt(GoldKey, 0);
            int enhancementStones = PlayerPrefs.GetInt(EnhancementStonesKey, 0);
            int chapter = PlayerPrefs.GetInt(ChapterKey, 1);
            int stageNumber = PlayerPrefs.GetInt(StageNumberKey, 1);
            int highestClearedChapter = PlayerPrefs.GetInt(HighestClearedChapterKey, 0);
            int highestClearedStageNumber = PlayerPrefs.GetInt(HighestClearedStageNumberKey, 0);
            long lastActiveUnixTime = long.Parse(PlayerPrefs.GetString(LastActiveUnixTimeKey, "0"));
            int attackPowerLevel = PlayerPrefs.GetInt(AttackPowerLevelKey, 0);
            int maxHealthLevel = PlayerPrefs.GetInt(MaxHealthLevelKey, 0);
            string inventoryJson = PlayerPrefs.GetString(InventoryJsonKey, "");
            int rankIndex = PlayerPrefs.GetInt(RankIndexKey, 0);

            return new SaveData(gold, enhancementStones, chapter, stageNumber, highestClearedChapter, highestClearedStageNumber, lastActiveUnixTime, attackPowerLevel, maxHealthLevel, inventoryJson, rankIndex);
        }

        /// <summary>
        /// 지금까지 추적한 값과 현재 시각을 PlayerPrefs에 즉시 기록한다.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetInt(GoldKey, _gold);
            PlayerPrefs.SetInt(EnhancementStonesKey, _enhancementStones);
            PlayerPrefs.SetInt(ChapterKey, _chapter);
            PlayerPrefs.SetInt(StageNumberKey, _stageNumber);
            PlayerPrefs.SetInt(HighestClearedChapterKey, _highestClearedChapter);
            PlayerPrefs.SetInt(HighestClearedStageNumberKey, _highestClearedStageNumber);
            PlayerPrefs.SetString(LastActiveUnixTimeKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.SetInt(AttackPowerLevelKey, _attackPowerLevel);
            PlayerPrefs.SetInt(MaxHealthLevelKey, _maxHealthLevel);
            PlayerPrefs.SetString(InventoryJsonKey, _inventoryJson);
            PlayerPrefs.SetInt(RankIndexKey, _rankIndex);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// save.InventoryJson으로 보유 장비/장착 상태를 복원한다. GameBootstrapper.Awake()에서
        /// InventoryService/EquippedGearService 생성 직후, Load() 결과를 넘겨 한 번 호출한다.
        /// </summary>
        public void RestoreInventory(SaveData save)
        {
            if (string.IsNullOrEmpty(save.InventoryJson))
            {
                return;
            }

            InventorySaveBlob blob = JsonUtility.FromJson<InventorySaveBlob>(save.InventoryJson);

            if (blob == null)
            {
                return;
            }

            _inventory.RestoreSnapshot(blob.Owned, _equipmentCatalog);
            _equippedGear.RestoreSnapshot(blob.Equipped, _equipmentCatalog, _inventory);
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            _gold = evt.CurrentGold;
            Save();
        }

        private void OnEnhancementStoneChanged(EnhancementStoneChangedEvent evt)
        {
            _enhancementStones = evt.CurrentStones;
            Save();
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _chapter = evt.Chapter;
            _stageNumber = evt.StageNumber;
            Save();
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            _highestClearedChapter = evt.Chapter;
            _highestClearedStageNumber = evt.StageNumber;
            Save();
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
            }

            Save();
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            RebuildInventorySnapshot();
            Save();
        }

        private void OnEquipmentEquipped(EquipmentEquippedEvent evt)
        {
            RebuildInventorySnapshot();
            Save();
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            _rankIndex = evt.NewRankIndex;
            Save();
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
    }
}
