using Character.Events;
using Core;
using Loot;
using Stage;
using Stage.Events;
using UnityEngine;

namespace Gacha
{
    /// <summary>
    /// 몬스터를 처치할 때마다 극희귀 확률로 무기/병사/스킬 뽑기권(주문서) 중 하나를 지급한다.
    /// 기준 확률(BaseChance)은 "본인이 역대 최고로 클리어한 스테이지"에서만 적용되고, 그보다
    /// 낮은(반복 모드 등으로 뒤처진) 스테이지에서 잡을수록 한 칸당 ChancePerStageBelow씩 줄어든다
    /// (0이 되면 그 밑으로는 드롭되지 않음) - 최고 기록 근처에서 플레이하는 것을 유도하고, 낮은
    /// 스테이지 파밍으로 이 보상을 우회하지 못하게 하기 위함이다. LootDropper와 동일한 구조
    /// (MonsterLootProvider 보유로 몬스터 판별, StageChangedEvent로 현재 스테이지 추적)이며,
    /// HighestStageClearedEvent로 최고 기록도 함께 추적한다.
    /// </summary>
    public sealed class RareGachaTicketDropService
    {
        private const float BaseChance = 0.00001f; // 0.001%
        private const float ChancePerStageBelow = 0.000001f; // 스테이지 한 칸당 0.0001%

        private readonly EventBus _events;
        private readonly StageCatalogSO _stageCatalog;
        private readonly EquipmentGachaTicketService _equipmentTicketService;
        private readonly SoldierTicketService _soldierTicketService;
        private readonly SkillScrollService _skillScrollService;

        private int _currentStageIndex = -1;
        private int _highestClearedStageIndex = -1;

        public RareGachaTicketDropService(
            EventBus events,
            StageCatalogSO stageCatalog,
            EquipmentGachaTicketService equipmentTicketService,
            SoldierTicketService soldierTicketService,
            SkillScrollService skillScrollService)
        {
            _events = events;
            _stageCatalog = stageCatalog;
            _equipmentTicketService = equipmentTicketService;
            _soldierTicketService = soldierTicketService;
            _skillScrollService = skillScrollService;

            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            _events.Subscribe<StageChangedEvent>(OnStageChanged);
            _events.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 게임 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            _events.Unsubscribe<StageChangedEvent>(OnStageChanged);
            _events.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
        }

        /// <summary>
        /// 세이브의 역대 최고 기록으로 확률 계산용 캐시를 조용히(이벤트 없이) 시딩한다.
        /// GameBootstrapper.Awake()에서 SaveService.Load() 직후 호출한다.
        /// </summary>
        public void SeedHighestCleared(int chapter, int stageNumber)
        {
            StageSO stage = _stageCatalog != null ? _stageCatalog.Find(chapter, stageNumber) : null;

            if (stage != null)
            {
                _highestClearedStageIndex = _stageCatalog.IndexOf(stage);
            }
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            StageSO stage = _stageCatalog != null ? _stageCatalog.Find(evt.Chapter, evt.StageNumber) : null;
            _currentStageIndex = stage != null ? _stageCatalog.IndexOf(stage) : -1;
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            StageSO stage = _stageCatalog != null ? _stageCatalog.Find(evt.Chapter, evt.StageNumber) : null;

            if (stage != null)
            {
                _highestClearedStageIndex = _stageCatalog.IndexOf(stage);
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character == null || !evt.Character.TryGetComponent(out MonsterLootProvider _))
            {
                return;
            }

            if (_currentStageIndex < 0 || _highestClearedStageIndex < 0)
            {
                return;
            }

            int stagesBelow = Mathf.Max(0, _highestClearedStageIndex - _currentStageIndex);
            float chance = Mathf.Max(0f, BaseChance - stagesBelow * ChancePerStageBelow);

            if (chance <= 0f || Random.value >= chance)
            {
                return;
            }

            GrantRandomTicket();
        }

        private void GrantRandomTicket()
        {
            int roll = Random.Range(0, 3);

            switch (roll)
            {
                case 0:
                    _equipmentTicketService?.AddTickets(1);
                    break;
                case 1:
                    _soldierTicketService?.AddTickets(1);
                    break;
                case 2:
                    _skillScrollService?.AddScrolls(1);
                    break;
            }
        }
    }
}
