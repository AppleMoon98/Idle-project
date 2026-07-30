using Character.Events;
using Core;
using Equipment;
using Loot.Events;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// 캐릭터 사망 이벤트를 구독해, 죽은 대상이 몬스터(MonsterLootProvider 보유)이면
    /// 드롭 데이터에 따라 골드 획득 이벤트를 발행한다.
    /// </summary>
    public sealed class LootDropper
    {
        private readonly EventBus _events;

        public LootDropper(EventBus events)
        {
            _events = events;
            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 게임 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character == null || !evt.Character.TryGetComponent(out MonsterLootProvider provider))
            {
                return;
            }

            MonsterLootSO loot = provider.Loot;

            if (loot == null)
            {
                return;
            }

            DropGold(loot);
            DropEquipment(loot);
        }

        private void DropGold(MonsterLootSO loot)
        {
            int? amount = LootRoller.RollGold(loot);

            if (amount.HasValue)
            {
                _events.Publish(new GoldEarnedEvent(amount.Value));
            }
        }

        private void DropEquipment(MonsterLootSO loot)
        {
            foreach (EquipmentSO equipment in LootRoller.RollEquipment(loot))
            {
                _events.Publish(new ItemDroppedEvent(equipment));
            }
        }
    }
}
