using Character.Events;
using Core;
using Equipment;
using Loot.Events;
using Stage;
using Stage.Events;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// 캐릭터 사망 이벤트를 구독해, 죽은 대상이 몬스터(MonsterLootProvider 보유)이면
    /// 골드는 그 몬스터의 드롭 데이터로, 장비는 현재 스테이지의 드롭 테이블로 판정해 발행한다.
    /// 현재 스테이지는 StageChangedEvent를 구독해 추적한다(StageController를 직접 참조하지 않는다).
    /// </summary>
    public sealed class LootDropper
    {
        private readonly EventBus _events;
        private readonly StageCatalogSO _stageCatalog;
        private readonly StageDifficultyConfigSO _difficultyConfig;
        private StageSO _currentStage;

        public LootDropper(EventBus events, StageCatalogSO stageCatalog, StageDifficultyConfigSO difficultyConfig)
        {
            _events = events;
            _stageCatalog = stageCatalog;
            _difficultyConfig = difficultyConfig;

            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            _events.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 게임 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            _events.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _currentStage = _stageCatalog != null ? _stageCatalog.Find(evt.Chapter, evt.StageNumber) : null;
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character == null || !evt.Character.TryGetComponent(out MonsterLootProvider provider))
            {
                return;
            }

            MonsterLootSO loot = provider.Loot;

            if (loot != null)
            {
                DropGold(loot);
            }

            DropEquipment();
        }

        private void DropGold(MonsterLootSO loot)
        {
            float multiplier = _difficultyConfig != null && _currentStage != null
                ? _difficultyConfig.GetGoldMultiplier(_stageCatalog.IndexOf(_currentStage))
                : 1f;

            int? amount = LootRoller.RollGold(loot, multiplier);

            if (amount.HasValue)
            {
                _events.Publish(new GoldEarnedEvent(amount.Value));
            }
        }

        private void DropEquipment()
        {
            if (_currentStage == null)
            {
                return;
            }

            foreach (EquipmentSO equipment in LootRoller.RollEquipment(_currentStage.EquipmentDrops))
            {
                _events.Publish(new ItemDroppedEvent(equipment));
            }
        }
    }
}
