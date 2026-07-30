using System;
using Core;
using Loot.Events;
using Stage.Events;
using UnityEngine;

namespace Save
{
    /// <summary>
    /// 오프라인 보상 계산에 필요한 최소 게임 상태(골드, 현재 스테이지, 마지막 접속 시각)를
    /// PlayerPrefs에 저장/로드한다. 다른 도메인을 직접 참조하지 않고 이벤트만 구독한다.
    /// </summary>
    public sealed class SaveService : IManager, IService
    {
        private const string GoldKey = "Save.Gold";
        private const string ChapterKey = "Save.Chapter";
        private const string StageNumberKey = "Save.StageNumber";
        private const string LastActiveUnixTimeKey = "Save.LastActiveUnixTime";

        private readonly EventBus _events;
        private int _gold;
        private int _chapter = 1;
        private int _stageNumber = 1;

        public SaveService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events.Subscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        /// <summary>
        /// 저장된 데이터를 읽는다. 저장 기록이 없으면 LastActiveUnixTime이 0인 기본값을 반환한다.
        /// </summary>
        public SaveData Load()
        {
            int gold = PlayerPrefs.GetInt(GoldKey, 0);
            int chapter = PlayerPrefs.GetInt(ChapterKey, 1);
            int stageNumber = PlayerPrefs.GetInt(StageNumberKey, 1);
            long lastActiveUnixTime = long.Parse(PlayerPrefs.GetString(LastActiveUnixTimeKey, "0"));

            return new SaveData(gold, chapter, stageNumber, lastActiveUnixTime);
        }

        /// <summary>
        /// 지금까지 추적한 값과 현재 시각을 PlayerPrefs에 즉시 기록한다.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetInt(GoldKey, _gold);
            PlayerPrefs.SetInt(ChapterKey, _chapter);
            PlayerPrefs.SetInt(StageNumberKey, _stageNumber);
            PlayerPrefs.SetString(LastActiveUnixTimeKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }

        private void OnGoldChanged(GoldChangedEvent evt)
        {
            _gold = evt.CurrentGold;
            Save();
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _chapter = evt.Chapter;
            _stageNumber = evt.StageNumber;
            Save();
        }
    }
}
