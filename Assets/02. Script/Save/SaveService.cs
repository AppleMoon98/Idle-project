using System;
using Core;
using Equipment.Events;
using Loot.Events;
using Stage.Events;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// 오프라인 보상 계산에 필요한 최소 게임 상태(재화, 현재/최고 스테이지, 마지막 접속 시각)를
    /// PlayerPrefs에 저장/로드한다. 다른 도메인을 직접 참조하지 않고 이벤트만 구독한다.
    /// </summary>
    public sealed class SaveService : IManager, IService
    {
        private const string GoldKey = "Save.Gold";
        private const string EnhancementStonesKey = "Save.EnhancementStones";
        private const string ChapterKey = "Save.Chapter";
        private const string StageNumberKey = "Save.StageNumber";
        private const string HighestClearedChapterKey = "Save.HighestClearedChapter";
        private const string HighestClearedStageNumberKey = "Save.HighestClearedStageNumber";
        private const string LastActiveUnixTimeKey = "Save.LastActiveUnixTime";

        private readonly EventBus _events;
        private int _gold;
        private int _enhancementStones;
        private int _chapter = 1;
        private int _stageNumber = 1;
        private int _highestClearedChapter;
        private int _highestClearedStageNumber;

        public SaveService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events.Subscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Subscribe<EnhancementStoneChangedEvent>(OnEnhancementStoneChanged);
            _events.Subscribe<StageChangedEvent>(OnStageChanged);
            _events.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Unsubscribe<EnhancementStoneChangedEvent>(OnEnhancementStoneChanged);
            _events.Unsubscribe<StageChangedEvent>(OnStageChanged);
            _events.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
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

            return new SaveData(gold, enhancementStones, chapter, stageNumber, highestClearedChapter, highestClearedStageNumber, lastActiveUnixTime);
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
            PlayerPrefs.Save();
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
    }
}
