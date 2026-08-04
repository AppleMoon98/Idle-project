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
        /// 세이브 직렬화용 스냅샷 한 줄. 빈 슬롯은 포함하지 않는다.
        /// </summary>
        [Serializable]
        public struct SkillLoadoutSnapshotEntry
        {
            public int SlotIndex;
            public int CatalogIndex;
        }

        private readonly EventBus _events;
        private readonly SkillService _skillService;
        private readonly SkillSO[] _slots = new SkillSO[SlotCount];
        private readonly bool[] _enabled;

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
        /// slotIndex가 자동 발동 대상인지. 잘못된 인덱스면 false.
        /// </summary>
        public bool IsEnabled(int slotIndex)
        {
            return IsValidSlot(slotIndex) && _enabled[slotIndex];
        }

        /// <summary>
        /// slotIndex의 자동 발동 켜짐/꺼짐을 지정한다. 실제로 값이 바뀔 때만 이벤트를 발행한다.
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
        /// slotIndex의 켜짐/꺼짐을 반전시킨다. HUD의 슬롯 클릭이 호출한다.
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

                int catalogIndex = catalog.IndexOf(_slots[i]);

                if (catalogIndex < 0)
                {
                    continue;
                }

                result.Add(new SkillLoadoutSnapshotEntry { SlotIndex = i, CatalogIndex = catalogIndex });
            }

            return result.ToArray();
        }

        /// <summary>
        /// 저장된 스냅샷으로 장착 상태를 복원한다. 시딩이라 이벤트를 발행하지 않는다
        /// (InventoryService.RestoreSnapshot과 동일한 이유).
        /// </summary>
        public void RestoreSnapshot(SkillLoadoutSnapshotEntry[] snapshot, SkillCatalogSO catalog)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (SkillLoadoutSnapshotEntry entry in snapshot)
            {
                if (!IsValidSlot(entry.SlotIndex))
                {
                    continue;
                }

                SkillSO definition = catalog.GetAt(entry.CatalogIndex);

                if (definition == null)
                {
                    continue;
                }

                _slots[entry.SlotIndex] = definition;
            }
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
        /// 저장된 꺼진 슬롯 목록으로 켜짐/꺼짐 상태를 복원한다. 시딩이라 이벤트를 발행하지 않는다.
        /// </summary>
        public void RestoreDisabledSlots(int[] disabledSlotIndices)
        {
            if (disabledSlotIndices == null)
            {
                return;
            }

            foreach (int slotIndex in disabledSlotIndices)
            {
                if (IsValidSlot(slotIndex))
                {
                    _enabled[slotIndex] = false;
                }
            }
        }
    }
}
