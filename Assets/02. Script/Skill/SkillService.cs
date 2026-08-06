using System;
using System.Collections.Generic;
using Core;
using Equipment;
using Loot;
using Skill.Events;

namespace Skill
{
    /// <summary>
    /// 스킬별 레벨을 관리한다. 레벨업은 골드와 강화석을 동시에 요구하며(둘 다 충분해야 성공),
    /// 실제 효과 적용은 하지 않는다 — SkillSlot이 이 서비스에서 레벨만 조회해 스스로 발동한다
    /// (EnhancementService/StatEnhancementReceiver와 동일한 "서비스는 상태만, 컴포넌트는 적용만" 분리).
    /// CurrencyService/EnhancementStoneService는 생성자로 주입받지 않고 TryLevelUp 시점에
    /// ServiceLocator에서 조회한다 — SaveService가 스냅샷 저장을 위해 이 서비스를 생성자로
    /// 주입받아야 하는데, GameBootstrapper에서 Currency/EnhancementStone은 SaveService.Load() 이후에
    /// (저장된 값으로) 생성되므로 그보다 먼저 존재해야 하는 SkillService가 그 둘을 생성자로
    /// 요구하면 순환 의존이 생긴다.
    /// </summary>
    public sealed class SkillService : IManager, IService
    {
        /// <summary>
        /// 세이브 직렬화용 스냅샷 한 줄. SkillCatalogSO 인덱스로 SkillSO를 식별한다
        /// (InventoryService.OwnedEquipmentSnapshot과 동일한 방식).
        /// </summary>
        [Serializable]
        public struct SkillLevelSnapshot
        {
            public int CatalogIndex;
            public int Level;
        }

        private readonly EventBus _events;
        private readonly Dictionary<SkillSO, int> _levels = new();

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
        /// 골드/강화석 소모 없이 레벨을 1 올린다(스킬 뽑기 전용 — TryLevelUp과 달리 재화를
        /// 요구하지 않는다). 이미 최대 레벨이면 아무 변화 없이 false.
        /// </summary>
        public bool LevelUpFree(SkillSO definition)
        {
            if (definition == null || IsMaxLevel(definition))
            {
                return false;
            }

            int newLevel = GetLevel(definition) + 1;
            _levels[definition] = newLevel;
            _events.Publish(new SkillLeveledUpEvent(definition, newLevel));

            return true;
        }

        /// <summary>
        /// 레벨업을 시도한다. 최대 레벨이거나 골드/강화석 중 하나라도 부족하면 아무 변화 없이 false.
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

            int newLevel = level + 1;
            _levels[definition] = newLevel;
            _events.Publish(new SkillLeveledUpEvent(definition, newLevel));

            return true;
        }

        /// <summary>
        /// 세이브 로드 시 저장된 레벨로 복원한다. 재화 소모/이벤트 발행 없이 상태만 맞춘다(시딩).
        /// </summary>
        public SkillLevelSnapshot[] ExportSnapshot(SkillCatalogSO catalog)
        {
            var snapshot = new List<SkillLevelSnapshot>();

            foreach (KeyValuePair<SkillSO, int> entry in _levels)
            {
                int catalogIndex = catalog.IndexOf(entry.Key);

                if (catalogIndex < 0)
                {
                    continue;
                }

                snapshot.Add(new SkillLevelSnapshot { CatalogIndex = catalogIndex, Level = entry.Value });
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
                SkillSO definition = catalog.GetAt(entry.CatalogIndex);

                if (definition == null)
                {
                    continue;
                }

                _levels[definition] = entry.Level;
            }
        }
    }
}
