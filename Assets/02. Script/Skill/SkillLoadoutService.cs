using System;
using System.Collections.Generic;
using Core;
using Skill.Events;

namespace Skill
{
    /// <summary>
    /// 6개의 고정 슬롯에 "레벨을 올려 보유한" 스킬 중 무엇을 실제로 자동 시전시킬지(장착) 관리한다.
    /// 레벨(SkillService, 재화 소모/영구)과 장착(이 서비스, 무료/자유 교체)은 완전히 분리된 개념이다 —
    /// Equipment의 "보유 vs 장착" 분리와 같은 철학. 실제 시전은 SkillSlot이 매 틱 GetEquipped로
    /// 조회해서 스스로 수행한다.
    /// </summary>
    public sealed class SkillLoadoutService : IManager, IService
    {
        /// <summary>
        /// 장착 슬롯 수.
        /// </summary>
        public const int SlotCount = 6;

        /// <summary>
        /// 세이브 직렬화용 스냅샷 한 줄. 빈 슬롯은 포함하지 않는다. 배열 인덱스 대신 StableId를
        /// 쓰는 이유는 GitHub 이슈 #19.
        /// </summary>
        [Serializable]
        public struct SkillLoadoutSnapshotEntry
        {
            public int SlotIndex;
            public string StableId;
        }

        private readonly EventBus _events;
        private readonly SkillService _skillService;
        private readonly SkillSO[] _slots = new SkillSO[SlotCount];
        private readonly bool[] _enabled;
        private readonly List<SkillSlot> _activeSlots = new List<SkillSlot>();

        public SkillLoadoutService(EventBus events, SkillService skillService)
        {
            _events = events;
            _skillService = skillService;

            _enabled = new bool[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                _enabled[i] = true;
            }
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// slotIndex에 장착된 스킬. 비어있으면 null.
        /// </summary>
        public SkillSO GetEquipped(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? _slots[slotIndex] : null;
        }

        /// <summary>
        /// definition을 slotIndex에 장착한다. 1레벨 미만이면 실패. 이미 다른 슬롯에 장착돼 있으면
        /// 그 슬롯에서 먼저 자동으로 빼서(중복 장착 방지) 옮긴다.
        /// </summary>
        public bool TryEquip(int slotIndex, SkillSO definition)
        {
            if (!IsValidSlot(slotIndex) || definition == null || _skillService.GetLevel(definition) < 1)
            {
                return false;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (i != slotIndex && _slots[i] == definition)
                {
                    _slots[i] = null;
                    _events.Publish(new SkillLoadoutChangedEvent(i, null));
                }
            }

            _slots[slotIndex] = definition;
            _events.Publish(new SkillLoadoutChangedEvent(slotIndex, definition));

            return true;
        }

        /// <summary>
        /// slotIndex의 장착을 해제한다. 이미 비어있으면 아무 일도 하지 않는다.
        /// </summary>
        public void Unequip(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || _slots[slotIndex] == null)
            {
                return;
            }

            _slots[slotIndex] = null;
            _events.Publish(new SkillLoadoutChangedEvent(slotIndex, null));
        }

        /// <summary>
        /// 두 슬롯의 장착 스킬을 서로 맞바꾼다(Soldier.SoldierDeploymentService.Swap과 동일한 규칙) -
        /// 한쪽만 채워져 있으면 그쪽으로 이동하고 반대쪽은 빈 칸이 되며, 둘 다 채워져 있으면
        /// 완전히 자리가 바뀐다. 둘 다 비어있으면 아무 일도 하지 않는다(이벤트 발행 없음).
        /// </summary>
        public void Swap(int slotA, int slotB)
        {
            if (!IsValidSlot(slotA) || !IsValidSlot(slotB) || slotA == slotB)
            {
                return;
            }

            SkillSO atA = _slots[slotA];
            SkillSO atB = _slots[slotB];

            if (atA == null && atB == null)
            {
                return;
            }

            _slots[slotA] = atB;
            _slots[slotB] = atA;

            _events.Publish(new SkillLoadoutChangedEvent(slotA, atB));
            _events.Publish(new SkillLoadoutChangedEvent(slotB, atA));
        }

        /// <summary>
        /// slotIndex가 "자동" 상태인지("수동"이면 false). 자동/수동 둘 다 HUD 탭으로 수동 발동
        /// (SkillSlot.TryManualCast)은 항상 가능하다 - 이 값은 SkillSlot.Tick의 자동 발동 여부만
        /// 가른다. 잘못된 인덱스면 false.
        /// </summary>
        public bool IsEnabled(int slotIndex)
        {
            return IsValidSlot(slotIndex) && _enabled[slotIndex];
        }

        /// <summary>
        /// slotIndex의 자동/수동 상태를 지정한다. 실제로 값이 바뀔 때만 이벤트를 발행한다.
        /// </summary>
        public void SetEnabled(int slotIndex, bool enabled)
        {
            if (!IsValidSlot(slotIndex) || _enabled[slotIndex] == enabled)
            {
                return;
            }

            _enabled[slotIndex] = enabled;
            _events.Publish(new SkillSlotEnabledChangedEvent(slotIndex, enabled));
        }

        /// <summary>
        /// slotIndex의 자동/수동 상태를 반전시킨다. HUD의 슬롯 길게 누르기가 호출한다.
        /// </summary>
        public void ToggleEnabled(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
            {
                return;
            }

            SetEnabled(slotIndex, !_enabled[slotIndex]);
        }

        /// <summary>
        /// definition이 현재 장착돼 있는 슬롯 인덱스. 장착돼 있지 않으면 -1.
        /// </summary>
        public int FindEquippedSlot(SkillSO definition)
        {
            if (definition == null)
            {
                return -1;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == definition)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }

        /// <summary>
        /// SkillSlot 컴포넌트가 자기 OnEnable에서 스스로 등록한다(Soldier.SquadMovementSyncService의
        /// Register/Unregister와 동일한 관례) — ResetAllCooldowns이 실제 씬의 슬롯 인스턴스에 접근할
        /// 방법이 이것뿐이기 때문(장착 정보만 갖고 있는 이 서비스 자신은 쿨다운 진행 상태를 모른다).
        /// </summary>
        public void RegisterSlot(SkillSlot slot)
        {
            if (slot != null && !_activeSlots.Contains(slot))
            {
                _activeSlots.Add(slot);
            }
        }

        /// <summary>
        /// RegisterSlot과 짝을 이루는 해제 — SkillSlot.OnDisable에서 호출한다.
        /// </summary>
        public void UnregisterSlot(SkillSlot slot)
        {
            _activeSlots.Remove(slot);
        }

        /// <summary>
        /// 등록된 슬롯 전체(6개, 장착 여부 무관)의 쿨다운을 즉시 발동 가능 상태로 되돌린다.
        /// 던전 입장 시점에 Stage.StageController.ResetSkillCooldowns()가 호출한다.
        /// </summary>
        public void ResetAllCooldowns()
        {
            foreach (SkillSlot slot in _activeSlots)
            {
                if (slot != null)
                {
                    slot.ResetCooldownReady();
                }
            }
        }

        /// <summary>
        /// 현재 장착 상태 전체를 스냅샷으로 내보낸다(빈 슬롯 제외). SaveService가 저장 시 호출한다.
        /// </summary>
        public SkillLoadoutSnapshotEntry[] ExportSnapshot(SkillCatalogSO catalog)
        {
            var result = new List<SkillLoadoutSnapshotEntry>();

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null)
                {
                    continue;
                }

                string stableId = _slots[i].StableId;

                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                result.Add(new SkillLoadoutSnapshotEntry { SlotIndex = i, StableId = stableId });
            }

            return result.ToArray();
        }

        /// <summary>
        /// SkillLoadoutService.RestoreSnapshot의 폐기 건수를 구조화된 결과로 돌려준다
        /// (Soldier.SoldierRosterService.RestoreResult/Inventory.InventoryService.RestoreResult와
        /// 동일한 형태, GitHub 이슈 #26/#31/#32).
        /// </summary>
        public readonly struct RestoreResult
        {
            public readonly int RestoredCount;
            public readonly int DiscardedOutOfRangeSlot;
            public readonly int DiscardedMissingCatalogEntry;
            public readonly int DiscardedUnlearnedSkill;
            public readonly int DiscardedDuplicateDefinition;

            public RestoreResult(int restoredCount, int discardedOutOfRangeSlot, int discardedMissingCatalogEntry, int discardedUnlearnedSkill, int discardedDuplicateDefinition)
            {
                RestoredCount = restoredCount;
                DiscardedOutOfRangeSlot = discardedOutOfRangeSlot;
                DiscardedMissingCatalogEntry = discardedMissingCatalogEntry;
                DiscardedUnlearnedSkill = discardedUnlearnedSkill;
                DiscardedDuplicateDefinition = discardedDuplicateDefinition;
            }

            public int TotalDiscarded => DiscardedOutOfRangeSlot + DiscardedMissingCatalogEntry + DiscardedUnlearnedSkill + DiscardedDuplicateDefinition;

            public bool HasDiscardedEntries => TotalDiscarded > 0;
        }

        /// <summary>
        /// 저장된 스냅샷으로 장착 상태를 복원한다. 시딩이라 이벤트를 발행하지 않는다
        /// (InventoryService.RestoreSnapshot과 동일한 이유).
        ///
        /// GitHub 이슈 #32 - TryEquip이 강제하는 불변조건(레벨 1 이상, 슬롯당 서로 다른 스킬)을
        /// 복원 경로에서는 전혀 검증하지 않아, 손상된/수동 편집된 저장 데이터가 미습득 스킬이나
        /// 같은 스킬의 중복 장착을 그대로 런타임 상태로 만들 수 있었다(이슈의 실제 재현: 레벨 0인
        /// 스킬을 슬롯 0/1에 동시에 넣으면 TryEquipUnlearned=False인데도 restoredSlot0/1이 둘 다
        /// True). 매 호출마다 먼저 _slots를 전부 비워(연속 복원 시 이전 저장의 잔류 장착이 남지
        /// 않도록) SlotIndex 오름차순으로 정렬해 처리한다 - 정렬해야 "첫 항목 우선" 중복 정책이
        /// 저장 배열의 원래 순서와 무관하게 항상 낮은 슬롯 인덱스를 우선하는 결정적인 결과를 낸다.
        /// 항목마다 슬롯 범위, 카탈로그 존재, 레벨 1 이상, 이미 이번 복원에서 다른 슬롯에 배정된
        /// 스킬인지(FindEquippedSlot으로 조회 - 이미 커밋된 _slots를 그대로 재사용)를 순서대로
        /// 검사해 하나라도 어긋나면 그 항목만 통째로 버리고 나머지 유효 항목은 계속 복원한다.
        /// </summary>
        public RestoreResult RestoreSnapshot(SkillLoadoutSnapshotEntry[] snapshot, SkillCatalogSO catalog)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = null;
            }

            if (snapshot == null)
            {
                return new RestoreResult(0, 0, 0, 0, 0);
            }

            var ordered = new List<SkillLoadoutSnapshotEntry>(snapshot);
            ordered.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            int restoredCount = 0;
            int discardedOutOfRange = 0;
            int discardedMissingCatalog = 0;
            int discardedUnlearned = 0;
            int discardedDuplicate = 0;

            foreach (SkillLoadoutSnapshotEntry entry in ordered)
            {
                if (!IsValidSlot(entry.SlotIndex))
                {
                    discardedOutOfRange++;
                    continue;
                }

                SkillSO definition = catalog.FindByStableId(entry.StableId);

                if (definition == null)
                {
                    discardedMissingCatalog++;
                    continue;
                }

                if (_skillService.GetLevel(definition) < 1)
                {
                    discardedUnlearned++;
                    continue;
                }

                if (FindEquippedSlot(definition) >= 0)
                {
                    discardedDuplicate++;
                    continue;
                }

                _slots[entry.SlotIndex] = definition;
                restoredCount++;
            }

            return new RestoreResult(restoredCount, discardedOutOfRange, discardedMissingCatalog, discardedUnlearned, discardedDuplicate);
        }

        /// <summary>
        /// 꺼진 슬롯 인덱스만 스냅샷으로 내보낸다(기본값인 켜짐 슬롯은 생략). SaveService가 저장 시 호출한다.
        /// </summary>
        public int[] ExportDisabledSlots()
        {
            var result = new List<int>();

            for (int i = 0; i < SlotCount; i++)
            {
                if (!_enabled[i])
                {
                    result.Add(i);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// RestoreDisabledSlots의 폐기 건수를 구조화된 결과로 돌려준다(GitHub 이슈 #32).
        /// </summary>
        public readonly struct DisabledSlotsRestoreResult
        {
            public readonly int RestoredCount;
            public readonly int DiscardedOutOfRangeSlot;

            public DisabledSlotsRestoreResult(int restoredCount, int discardedOutOfRangeSlot)
            {
                RestoredCount = restoredCount;
                DiscardedOutOfRangeSlot = discardedOutOfRangeSlot;
            }

            public bool HasDiscardedEntries => DiscardedOutOfRangeSlot > 0;
        }

        /// <summary>
        /// 저장된 꺼진 슬롯 목록으로 켜짐/꺼짐 상태를 복원한다. 시딩이라 이벤트를 발행하지 않는다.
        ///
        /// GitHub 이슈 #32 - 매 호출마다 먼저 전체 슬롯을 기본값(켜짐)으로 되돌린 뒤 저장된
        /// 꺼진 슬롯만 다시 끈다 - RestoreSnapshot과 같은 이유로, 연속 복원 시 이전 저장의
        /// "꺼짐" 상태가 새 저장에 없는데도 잔류하는 것을 막는다.
        /// </summary>
        public DisabledSlotsRestoreResult RestoreDisabledSlots(int[] disabledSlotIndices)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _enabled[i] = true;
            }

            if (disabledSlotIndices == null)
            {
                return new DisabledSlotsRestoreResult(0, 0);
            }

            int restoredCount = 0;
            int discardedOutOfRange = 0;

            foreach (int slotIndex in disabledSlotIndices)
            {
                if (!IsValidSlot(slotIndex))
                {
                    discardedOutOfRange++;
                    continue;
                }

                _enabled[slotIndex] = false;
                restoredCount++;
            }

            return new DisabledSlotsRestoreResult(restoredCount, discardedOutOfRange);
        }
    }
}
