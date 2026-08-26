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
    /// 현재 스테이지는 StageChangedEvent를 구독해 추적한다.
    ///
    /// GitHub 이슈 #29 - 던전(골드/강화석/스킬/보스/병사 구출) 오버레이와 랭크 승급전은
    /// StageChangedEvent를 발행하지 않으므로, 오버레이 진입 전 마지막 일반 스테이지가
    /// _currentStage에 그대로 남는다. 이 오버레이들의 보스/몬스터 프리팹 대부분이 여전히
    /// MonsterLootProvider를 갖고 있어(강화석/스킬/보스/병사 구출 던전, 랭크 승급전 - 오직
    /// 골드 던전의 몬스터만 우연히 이 컴포넌트가 없어 안전했다), 던전 전용 보상(강화석/주문서/
    /// 토큰/티켓)과 별개로 "던전 진입 전 스테이지"의 일반 골드·장비까지 매 처치마다 추가로
    /// 굴려지고 있었다. 5개 던전과 승급전 전부 이미 자기 완결적인 전용 보상 체계를 갖고 있어
    /// 일반 드롭을 원하는 곳이 없으므로, StageController.IsOverlayActive(이미 모든 오버레이가
    /// 중복 진입 방지에 쓰는, "지금 어떤 오버레이든 활성 중인가"를 나타내는 단일 신호)가 켜져
    /// 있는 동안은 골드·장비 드롭 로직 자체를 건너뛴다 - 우연한 프리팹 컴포넌트 구성이 아니라
    /// 명시적인 정책으로 확정한다.
    /// </summary>
    public sealed class LootDropper
    {
        private readonly EventBus _events;
        private readonly StageCatalogSO _stageCatalog;
        private readonly StageDifficultyConfigSO _difficultyConfig;
        private readonly StageController _stageController;
        private StageSO _currentStage;

        public LootDropper(EventBus events, StageCatalogSO stageCatalog, StageDifficultyConfigSO difficultyConfig, StageController stageController)
        {
            _events = events;
            _stageCatalog = stageCatalog;
            _difficultyConfig = difficultyConfig;
            _stageController = stageController;

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
            if (_stageController != null && _stageController.IsOverlayActive)
            {
                return;
            }

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
