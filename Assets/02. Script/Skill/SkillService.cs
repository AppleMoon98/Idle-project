using System;
using System.Collections.Generic;
using Core;
using Equipment;
using Loot;
using Skill.Events;

namespace Skill
{
    /// <summary>
    /// 스킬별 레벨과 보유 개수(중복 획득분)를 관리한다. 스킬은 뽑기/던전으로 "개수"만 늘어나고,
    /// 그 개수를 재료로 소모해야 레벨업할 수 있다 — 0강(미습득) -> 1강은 개수 1개만 있으면
    /// 골드/강화석 없이 무료로 열리고, 그 이후(1강 이상)는 개수 3개 + 골드/강화석을 동시에
    /// 요구한다(둘 다 충분해야 성공). 실제 효과 적용은 하지 않는다 — SkillSlot이 이 서비스에서
    /// 레벨만 조회해 스스로 발동한다(EnhancementService/StatEnhancementReceiver와 동일한
    /// "서비스는 상태만, 컴포넌트는 적용만" 분리).
    /// CurrencyService/EnhancementStoneService는 생성자로 주입받지 않고 TryLevelUp 시점에
    /// ServiceLocator에서 조회한다 — SaveService가 스냅샷 저장을 위해 이 서비스를 생성자로
    /// 주입받아야 하는데, GameBootstrapper에서 Currency/EnhancementStone은 SaveService.Load() 이후에
    /// (저장된 값으로) 생성되므로 그보다 먼저 존재해야 하는 SkillService가 그 둘을 생성자로
    /// 요구하면 순환 의존이 생긴다.
    /// </summary>
    public sealed class SkillService : IManager, IService
    {
        /// <summary>
        /// 레벨 0(미습득) -> 1강에 필요한 보유 개수. 이 구간만 무료다.
        /// </summary>
        private const int FirstUnlockRequiredCount = 1;

        /// <summary>
        /// 1강 이상에서 다음 레벨로 올릴 때마다 소모되는 보유 개수.
        /// </summary>
        private const int PerLevelRequiredCount = 3;

        /// <summary>
        /// 세이브 직렬화용 스냅샷 한 줄. SkillSO.StableId로 SkillSO를 식별한다
        /// (InventoryService.OwnedEquipmentSnapshot과 동일한 방식, 배열 인덱스 대신 StableId를
        /// 쓰는 이유는 GitHub 이슈 #19).
        /// </summary>
        [Serializable]
        public struct SkillLevelSnapshot
        {
            public string StableId;
            public int Level;
        }

        /// <summary>
        /// 세이브 직렬화용 스냅샷 한 줄(보유 개수). SkillLevelSnapshot과 동일한 방식.
        /// </summary>
        [Serializable]
        public struct SkillCountSnapshot
        {
            public string StableId;
            public int Count;
        }

        private readonly EventBus _events;
        private readonly Dictionary<SkillSO, int> _levels = new();
        private readonly Dictionary<SkillSO, int> _counts = new();

        public SkillService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 스킬의 현재 레벨(미강화 상태면 0).
        /// </summary>
        public int GetLevel(SkillSO definition)
        {
            return definition != null && _levels.TryGetValue(definition, out int level) ? level : 0;
        }

        /// <summary>
        /// 스킬이 이미 최대 레벨인지 여부. SkillGachaService가 뽑기 후보에서 만렙 스킬을
        /// 제외할 때 쓴다.
        /// </summary>
        public bool IsMaxLevel(SkillSO definition)
        {
            return definition != null && GetLevel(definition) >= definition.MaxLevel;
        }

        /// <summary>
        /// 이 스킬을 현재 몇 개 보유 중인지(레벨업 재료로 아직 소모되지 않은 개수).
        /// </summary>
        public int GetCount(SkillSO definition)
        {
            return definition != null && _counts.TryGetValue(definition, out int count) ? count : 0;
        }

        /// <summary>
        /// 다음 레벨업(현재 레벨 -> +1)에 필요한 보유 개수. 0강(미습득) 구간만 1개, 그 이후는 3개.
        /// </summary>
        public int GetRequiredCount(SkillSO definition)
        {
            return GetLevel(definition) == 0 ? FirstUnlockRequiredCount : PerLevelRequiredCount;
        }

        /// <summary>
        /// 스킬 뽑기/던전 등 획득 경로에서 호출한다 — 보유 개수만 늘리고 레벨/재화는 건드리지 않는다.
        /// </summary>
        public void AddCopy(SkillSO definition, int amount = 1)
        {
            if (definition == null || amount <= 0)
            {
                return;
            }

            int newCount = GetCount(definition) + amount;
            _counts[definition] = newCount;
            _events.Publish(new SkillCountChangedEvent(definition, newCount));
        }

        /// <summary>
        /// 레벨업을 시도한다. 최대 레벨이거나, 보유 개수가 모자라거나(0강 구간 1개 / 그 이후 3개),
        /// 1강 이상 구간에서 골드/강화석 중 하나라도 부족하면 아무 변화 없이 false.
        /// 0강 -> 1강은 개수만 소모하고 골드/강화석은 요구하지 않는다.
        /// </summary>
        public bool TryLevelUp(SkillSO definition)
        {
            if (definition == null)
            {
                return false;
            }

            int level = GetLevel(definition);

            if (level >= definition.MaxLevel)
            {
                return false;
            }

            int requiredCount = GetRequiredCount(definition);

            if (GetCount(definition) < requiredCount)
            {
                return false;
            }

            bool isFirstUnlock = level == 0;

            if (!isFirstUnlock)
            {
                if (GameBootstrapper.Services == null
                    || !GameBootstrapper.Services.TryGet(out CurrencyService currency)
                    || !GameBootstrapper.Services.TryGet(out EnhancementStoneService stones))
                {
                    return false;
                }

                int goldCost = definition.GetGoldCost(level);
                int stoneCost = definition.GetStoneCost(level);

                if (currency.CurrentGold < goldCost || stones.CurrentStones < stoneCost)
                {
                    return false;
                }

                currency.TrySpendGold(goldCost);
                stones.TrySpendStones(stoneCost);
            }

            int newCount = GetCount(definition) - requiredCount;
            _counts[definition] = newCount;
            _events.Publish(new SkillCountChangedEvent(definition, newCount));

            int newLevel = level + 1;
            _levels[definition] = newLevel;
            _events.Publish(new SkillLeveledUpEvent(definition, newLevel));

            return true;
        }

        /// <summary>
        /// catalog의 모든 스킬을 순서대로 순회하며, 각 스킬을 조건(보유 개수 + 1강 이상이면
        /// 골드/강화석)이 허용하는 한 TryLevelUp을 반복해 최대한 레벨업시킨다 —
        /// Equipment.EquipmentEnhancementService.TryEnhanceAll과 동일한 "한 항목을 소진할 때까지
        /// 반복한 뒤 다음으로" 관례. 성공한 총 레벨업 횟수를 반환한다.
        /// </summary>
        public int TryLevelUpAll(SkillCatalogSO catalog)
        {
            int successCount = 0;

            if (catalog == null || catalog.Skills == null)
            {
                return successCount;
            }

            foreach (SkillSO definition in catalog.Skills)
            {
                while (TryLevelUp(definition))
                {
                    successCount++;
                }
            }

            return successCount;
        }

        /// <summary>
        /// 세이브 로드 시 저장된 레벨로 복원한다. 재화 소모/이벤트 발행 없이 상태만 맞춘다(시딩).
        /// </summary>
        public SkillLevelSnapshot[] ExportSnapshot(SkillCatalogSO catalog)
        {
            var snapshot = new List<SkillLevelSnapshot>();

            foreach (KeyValuePair<SkillSO, int> entry in _levels)
            {
                string stableId = entry.Key.StableId;

                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                snapshot.Add(new SkillLevelSnapshot { StableId = stableId, Level = entry.Value });
            }

            return snapshot.ToArray();
        }

        public void RestoreSnapshot(SkillLevelSnapshot[] snapshot, SkillCatalogSO catalog)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (SkillLevelSnapshot entry in snapshot)
            {
                SkillSO definition = catalog.FindByStableId(entry.StableId);

                if (definition == null)
                {
                    continue;
                }

                _levels[definition] = entry.Level;
            }
        }

        /// <summary>
        /// 세이브 로드 시 저장된 보유 개수로 복원한다. 이벤트 발행 없이 상태만 맞춘다(시딩).
        /// ExportSnapshot/RestoreSnapshot(레벨)과 동일한 방식.
        /// </summary>
        public SkillCountSnapshot[] ExportCountSnapshot(SkillCatalogSO catalog)
        {
            var snapshot = new List<SkillCountSnapshot>();

            foreach (KeyValuePair<SkillSO, int> entry in _counts)
            {
                string stableId = entry.Key.StableId;

                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }

                snapshot.Add(new SkillCountSnapshot { StableId = stableId, Count = entry.Value });
            }

            return snapshot.ToArray();
        }

        public void RestoreCountSnapshot(SkillCountSnapshot[] snapshot, SkillCatalogSO catalog)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (SkillCountSnapshot entry in snapshot)
            {
                SkillSO definition = catalog.FindByStableId(entry.StableId);

                if (definition == null)
                {
                    continue;
                }

                _counts[definition] = entry.Count;
            }
        }
    }
}
