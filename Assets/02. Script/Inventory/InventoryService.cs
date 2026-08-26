using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Equipment;
using Inventory.Events;
using Loot.Events;

namespace Inventory
{
    /// <summary>
    /// 드롭된 장비를 라인(OwnedEquipment) 단위로 보관하는 서비스. ItemDroppedEvent를 구독해
    /// 이미 보유 중인 장비면 스택 카운트를 올리고, 처음 보는 장비면 새 라인을 추가한다.
    /// 변경 시 InventoryChangedEvent를 발행해 UI 등이 구독할 수 있게 한다.
    /// </summary>
    public sealed class InventoryService : IManager, IService
    {
        /// <summary>
        /// 보유 장비 한 라인을 세이브 데이터로 직렬화하기 위한 형태. EquipmentSO 참조 대신
        /// EquipmentSO.StableId로 "어떤 장비인지"를 기록한다(PlayerPrefs는 에셋 참조를 담을 수 없음).
        /// 배열 인덱스 대신 StableId를 쓰는 이유는 GitHub 이슈 #19 참고 - 카탈로그 재정렬/삭제 시
        /// 인덱스가 밀려 다른 항목을 가리키는 문제를 막는다.
        /// </summary>
        [Serializable]
        public struct OwnedEquipmentSnapshot
        {
            public string StableId;
            public int Count;
            public int EnhancementLevel;
        }

        private readonly EventBus _events;
        private readonly Dictionary<EquipmentSO, OwnedEquipment> _owned = new();

        /// <summary>
        /// 현재 보유 중인 장비 라인 목록 (읽기 전용).
        /// </summary>
        public IReadOnlyCollection<OwnedEquipment> Items => _owned.Values;

        public InventoryService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events.Subscribe<ItemDroppedEvent>(OnItemDropped);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<ItemDroppedEvent>(OnItemDropped);
        }

        /// <summary>
        /// definition을 현재 보유 중인 라인을 반환한다. 보유하고 있지 않으면 false.
        /// </summary>
        public bool TryGet(EquipmentSO definition, out OwnedEquipment owned)
        {
            return _owned.TryGetValue(definition, out owned);
        }

        /// <summary>
        /// definition을 amount개 소모한다(합성/강화 재료). 보유량이 부족하면 아무 변화 없이 false.
        /// 0개가 되어도 라인 자체는 제거하지 않고 그대로 둔다 - 한 번이라도 획득한 장비는 개수가
        /// 0이 되어도 목록에 남아 장착 가능해야 한다는 정책(EquipmentSlotPopupUI/EquippedGearService
        /// 참고) 때문에, "보유한 적이 있다"는 사실 자체를 잃지 않는다.
        ///
        /// GitHub 이슈 #31 - amount가 0 이하(음수 포함)면 즉시 거부한다. 기존에는 이 검사가 없어
        /// 음수 amount가 owned.Count &lt; amount 검사를 그대로 통과한 뒤 owned.Count -= amount가
        /// 오히려 스택을 늘려(재료 무한 복제) 버렸다 - 합성/강화 서비스가 SO에 설정된 값(예:
        /// EquipmentEnhancementConfigSO.DuplicatesRequiredPerLevel)을 그대로 넘기므로, 그 SO가
        /// 잘못 설정돼도(예: 음수) 이 메서드 자신이 최종 방어선이 되어야 한다는 게 이슈의 핵심
        /// 요구다. amount &lt;= 0을 여기서 걸러두면 amount=int.MinValue로 인한 뺄셈 오버플로 경로도
        /// 함께 막힌다(양수 amount만 owned.Count -= amount에 도달하고, 그 시점엔 이미 owned.Count
        /// &gt;= amount가 확인돼 있어 결과가 항상 0 이상이라 오버플로할 수 없다).
        /// </summary>
        public bool TryConsume(EquipmentSO definition, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (!_owned.TryGetValue(definition, out OwnedEquipment owned) || owned.Count < amount)
            {
                return false;
            }

            owned.Count -= amount;

            _events.Publish(new InventoryChangedEvent(owned, _owned.Count));
            return true;
        }

        /// <summary>
        /// definition 라인의 강화 레벨을 levels만큼 올린다. 보유하고 있지 않으면 아무 일도 하지 않는다.
        ///
        /// GitHub 이슈 #31 - levels가 0 이하면 조용히 무시한다(CurrencyService.AddGold 등이 이미
        /// 쓰는 "Add*는 잘못된 입력에 조용히 no-op" 관례, GitHub 이슈 #8과 동일 방향 - 이 메서드는
        /// TryConsume과 달리 "시도가 실패할 수 있는" API가 아니라 순수 지급 API라 bool을 반환하지
        /// 않는다). 덧셈은 long으로 계산한 뒤 int.MaxValue로 saturate한다 - Equipment.
        /// EquipmentEnhancementService.GetNextStoneCost가 이미 쓰는 것과 같은 오버플로 방지 관례.
        /// </summary>
        public void AddEnhancementLevel(EquipmentSO definition, int levels)
        {
            if (levels <= 0)
            {
                return;
            }

            if (!_owned.TryGetValue(definition, out OwnedEquipment owned))
            {
                return;
            }

            long newLevel = (long)owned.EnhancementLevel + levels;
            owned.EnhancementLevel = (int)Math.Min(newLevel, int.MaxValue);

            _events.Publish(new InventoryChangedEvent(owned, _owned.Count));
        }

        /// <summary>
        /// slot에 속한 보유 라인을 등급 낮은 것부터 정렬해 반환한다. "슬롯 전체 일괄 처리" 기능
        /// (EquipmentEnhancementService.TryEnhanceAll, EquipmentFusionService.TryFuseAll)이 공유하는 조회 로직.
        /// </summary>
        public List<EquipmentSO> GetLinesBySlotSortedByGrade(EquipmentType slot, EquipmentGradeCatalogSO gradeCatalog)
        {
            return _owned.Values
                .Where(owned => owned.Definition.EquipmentType == slot)
                .OrderBy(owned => gradeCatalog.IndexOf(owned.Definition.Grade))
                .Select(owned => owned.Definition)
                .ToList();
        }

        /// <summary>
        /// 현재 보유 장비 전체를 세이브용 스냅샷으로 내보낸다. catalog에 없는(콘텐츠 삭제된) 항목은 건너뛴다.
        /// </summary>
        public OwnedEquipmentSnapshot[] ExportSnapshot(EquipmentCatalogSO catalog)
        {
            var snapshot = new List<OwnedEquipmentSnapshot>();

            foreach (OwnedEquipment owned in _owned.Values)
            {
                string stableId = owned.Definition.StableId;

                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                snapshot.Add(new OwnedEquipmentSnapshot
                {
                    StableId = stableId,
                    Count = owned.Count,
                    EnhancementLevel = owned.EnhancementLevel
                });
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// InventoryService.RestoreSnapshot의 폐기 건수를 구조화된 결과로 돌려준다
        /// (Soldier.SoldierRosterService.RestoreResult와 동일한 형태, GitHub 이슈 #26/#31).
        /// </summary>
        public readonly struct RestoreResult
        {
            public readonly int RestoredCount;
            public readonly int DiscardedMissingCatalogEntry;
            public readonly int DiscardedNegativeCount;
            public readonly int DiscardedNegativeEnhancementLevel;

            public RestoreResult(int restoredCount, int discardedMissingCatalogEntry, int discardedNegativeCount, int discardedNegativeEnhancementLevel)
            {
                RestoredCount = restoredCount;
                DiscardedMissingCatalogEntry = discardedMissingCatalogEntry;
                DiscardedNegativeCount = discardedNegativeCount;
                DiscardedNegativeEnhancementLevel = discardedNegativeEnhancementLevel;
            }

            public int TotalDiscarded => DiscardedMissingCatalogEntry + DiscardedNegativeCount + DiscardedNegativeEnhancementLevel;

            public bool HasDiscardedEntries => TotalDiscarded > 0;
        }

        /// <summary>
        /// 세이브 스냅샷으로 보유 장비를 복원한다. 게임플레이 획득이 아니므로 InventoryChangedEvent는 발행하지 않는다.
        ///
        /// GitHub 이슈 #31 - 손상된 저장 데이터(음수 Count/EnhancementLevel)가 그대로 런타임 상태가
        /// 되는 것을 막는다. Count/EnhancementLevel 중 하나라도 음수인 항목, 카탈로그에 없는 항목은
        /// 통째로 버리고(부분 클램프가 아니라 완전 폐기 - SoldierRosterService.RestoreSnapshot이
        /// 손상된 항목을 다루는 것과 동일한 "안전한 쪽으로 완전히 버림" 관례) 나머지 유효 항목은
        /// 그대로 계속 복원한다. Count/EnhancementLevel이 정확히 0인 항목은 유효하다(0은 정상
        /// 상태 - 위 TryConsume의 doc comment 참고).
        /// </summary>
        public RestoreResult RestoreSnapshot(OwnedEquipmentSnapshot[] snapshot, EquipmentCatalogSO catalog)
        {
            if (snapshot == null)
            {
                return new RestoreResult(0, 0, 0, 0);
            }

            int restoredCount = 0;
            int discardedMissingCatalog = 0;
            int discardedNegativeCount = 0;
            int discardedNegativeLevel = 0;

            foreach (OwnedEquipmentSnapshot entry in snapshot)
            {
                if (entry.Count < 0)
                {
                    discardedNegativeCount++;
                    continue;
                }

                if (entry.EnhancementLevel < 0)
                {
                    discardedNegativeLevel++;
                    continue;
                }

                EquipmentSO definition = catalog.FindByStableId(entry.StableId);

                if (definition == null)
                {
                    discardedMissingCatalog++;
                    continue;
                }

                _owned[definition] = new OwnedEquipment(definition, entry.Count, entry.EnhancementLevel);
                restoredCount++;
            }

            return new RestoreResult(restoredCount, discardedMissingCatalog, discardedNegativeCount, discardedNegativeLevel);
        }

        private void OnItemDropped(ItemDroppedEvent evt)
        {
            if (_owned.TryGetValue(evt.Equipment, out OwnedEquipment owned))
            {
                owned.Count++;
            }
            else
            {
                owned = new OwnedEquipment(evt.Equipment, count: 1, enhancementLevel: 0);
                _owned[evt.Equipment] = owned;
            }

            _events.Publish(new InventoryChangedEvent(owned, _owned.Count));
        }
    }
}
