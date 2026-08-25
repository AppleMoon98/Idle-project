using Core;
using Enhancement;
using Managers;
using Skill.Events;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 전장의 외침 - 플레이어 자신에게는 아무 효과가 없고, 병사들의 이동속도/공격속도만
    /// SkillSO.GetBuffDuration(레벨) 동안 올린다. SkillSelfBuffAppliedEvent를 스탯별로
    /// (이동속도, 공격속도) 하나씩 발행만 하고, 실제 적용은 병사(Soldier.SoldierStatReceiver)가
    /// 이 이벤트를 구독해 각자 처리한다 - 이 컴포넌트는 "병사"라는 개념을 전혀 모른다.
    /// 시전자 본인에게 적용할 대상이 없어 revert할 상태도 갖지 않는다(SelfBuffSkillEffect와
    /// 달리 ITickable이 필요 없다).
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class SoldierBuffSkillEffect : MonoBehaviour, ISkillEffect
    {
        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private PoolManager _pool;

        private void OnEnable()
        {
            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
            }
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            int level = GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService skillService)
                ? skillService.GetLevel(definition)
                : 0;
            float duration = definition.GetBuffDuration(level);

            GameBootstrapper.Events?.Publish(new SkillSelfBuffAppliedEvent(EnhancementStatType.MoveSpeed, magnitude, duration, definition));
            GameBootstrapper.Events?.Publish(new SkillSelfBuffAppliedEvent(EnhancementStatType.AttackSpeed, magnitude, duration, definition));

            Vector3 spawnPosition = origin.position + Vector3.up * definition.VfxHeightOffset;
            Transform followTarget = definition.VfxFollowCaster ? origin : null;
            SkillEffectVfx.SpawnAndPlay(_pool, definition, spawnPosition, vfxPoolCapacity, vfxPoolMaxSize, followTarget: followTarget);
        }

        /// <summary>
        /// 병사 전체에게 거는 버프라 공격 대상이 필요 없다 - 항상 발동 가능하다.
        /// </summary>
        public bool HasTargetInRange(Transform origin, SkillSO definition)
        {
            return true;
        }
    }
}
